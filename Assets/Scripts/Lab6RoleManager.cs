using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerRole : byte
{
    None = 0,
    Guardian = 1,
    Explorer = 2
}

public enum Lab6MsgKind : byte
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Danger = 3,
    Guardian = 4,
    Explorer = 5
}

public class Lab6RoleManager : MonoBehaviour
{
    public static Lab6RoleManager Instance { get; private set; }

    [Header("Cofre (lo crea el Guardian por solicitud)")]
    public NetworkObject chestPrefab;
    public Vector3 chestSpawnPosition = new Vector3(4.22f, 0f, 0.70711f);

    [Header("Aparicion de jugadores")]
    public Vector3 playerSpawnOrigin = new Vector3(1.7906f, 0.15f, 3.62181f);
    public float playerSpawnSpacing = 2f;

    [Header("HUD de verificacion")]
    public bool showHud = true;

    private readonly Dictionary<ulong, PlayerRole> m_roles = new Dictionary<ulong, PlayerRole>();
    private NetworkObject m_chestInstance;
    private bool m_initialized;
    private bool m_environmentBuilt;
    private int m_freeLootCount;
    private int m_ownLootCount;
    private float m_nextLootScan;
    private GameObject m_platform;
    private Light m_chestLight;

    public ulong GuardianClientId { get; private set; } = ulong.MaxValue;

    private const float MessageLifetime = 3.5f;
    private const int MaxMessages = 5;

    private struct HudMessage
    {
        public string Text;
        public Color Color;
        public float Expiry;
    }

    private static readonly List<HudMessage> s_messages = new List<HudMessage>();

    private static GUIStyle s_panelStyle;
    private static GUIStyle s_titleStyle;
    private static GUIStyle s_labelStyle;
    private static GUIStyle s_roleStyle;
    private static GUIStyle s_smallStyle;
    private static GUIStyle s_centerStyle;
    private static GUIStyle s_messageStyle;
    private static Texture2D s_panelTexture;

    public static readonly Color GuardianColor = new Color(1f, 0.76f, 0.18f);
    public static readonly Color ExplorerColor = new Color(0.28f, 0.82f, 1f);
    public static readonly Color FreeLootColor = new Color(0.62f, 0.62f, 0.66f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        s_messages.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        Initialize();
        BuildEnvironment();
    }

    private void Update()
    {
        Initialize();

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            HandleStartHotkeys(networkManager);
        }

        if (Time.unscaledTime >= m_nextLootScan)
        {
            m_nextLootScan = Time.unscaledTime + 0.4f;
            ScanLoot(out m_ownLootCount, out m_freeLootCount);
        }

        UpdateChestDecoration();
    }

    private void UpdateChestDecoration()
    {
        if (m_platform == null && m_chestLight == null) return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.SpawnManager == null) return;

        foreach (NetworkObject networkObject in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!networkObject.TryGetComponent(out Chest chest)) continue;

            Vector3 position = chest.transform.position;
            if (m_platform != null)
            {
                m_platform.transform.position = new Vector3(position.x, -0.62f, position.z);
            }

            if (m_chestLight != null)
            {
                m_chestLight.transform.position = position + new Vector3(0f, 2.6f, 0f);
            }

            return;
        }
    }

    private void HandleStartHotkeys(NetworkManager networkManager)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (!networkManager.IsListening)
        {
            if (keyboard.hKey.wasPressedThisFrame) networkManager.StartHost();
            else if (keyboard.cKey.wasPressedThisFrame) networkManager.StartClient();
        }
        else if (keyboard.xKey.wasPressedThisFrame)
        {
            networkManager.Shutdown();
        }
    }

    private void Initialize()
    {
        if (m_initialized) return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null) return;

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = OnConnectionApproval;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
        m_initialized = true;
        Debug.Log("[Lab6] Roles activados por codigo (ConnectionApproval + callback)");
    }

    private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        ulong clientId = request.ClientNetworkId;

        if (GuardianClientId == ulong.MaxValue)
        {
            GuardianClientId = clientId;
            m_roles[clientId] = PlayerRole.Guardian;
        }
        else
        {
            m_roles[clientId] = PlayerRole.Explorer;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = null;
        response.Position = GetPlayerSpawnPosition(clientId);
        response.Rotation = Quaternion.identity;

        Debug.Log($"[Lab6] Cliente {clientId} aprobado con rol {m_roles[clientId]}");
    }

    private Vector3 GetPlayerSpawnPosition(ulong clientId)
    {
        return playerSpawnOrigin + new Vector3(playerSpawnSpacing * (clientId % 8), 0f, 0f);
    }

    public PlayerRole ResolveRole(ulong clientId)
    {
        if (m_roles.TryGetValue(clientId, out PlayerRole role))
        {
            return role;
        }

        if (GuardianClientId == ulong.MaxValue)
        {
            GuardianClientId = clientId;
        }

        role = clientId == GuardianClientId ? PlayerRole.Guardian : PlayerRole.Explorer;
        m_roles[clientId] = role;
        return role;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.ShutdownInProgress) return;

        if (!networkManager.IsServer)
        {
            PostMessage("Desconectado del servidor", Lab6MsgKind.Danger);
            return;
        }

        Broadcast($"Jugador {clientId} desconectado", Lab6MsgKind.Warning);
        m_roles.Remove(clientId);

        if (GuardianClientId != clientId) return;

        GuardianClientId = ulong.MaxValue;
        ulong nuevoGuardian = ulong.MaxValue;
        foreach (KeyValuePair<ulong, PlayerRole> kv in m_roles)
        {
            if (kv.Key < nuevoGuardian)
            {
                nuevoGuardian = kv.Key;
            }
        }

        if (nuevoGuardian == ulong.MaxValue)
        {
            Broadcast("El Guardian se desconecto. No quedan jugadores.", Lab6MsgKind.Danger);
            return;
        }

        GuardianClientId = nuevoGuardian;
        m_roles[nuevoGuardian] = PlayerRole.Guardian;
        ApplyRole(nuevoGuardian, PlayerRole.Guardian);
        Broadcast($"El Guardian se desconecto. Jugador {nuevoGuardian} es el nuevo Guardian", Lab6MsgKind.Guardian);
        Debug.Log($"[Lab6] El Guardian se desconecto; el cliente {nuevoGuardian} pasa a ser Guardian");
    }

    private void ApplyRole(ulong clientId, PlayerRole role)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer) return;
        if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return;
        if (client.PlayerObject == null) return;

        if (client.PlayerObject.TryGetComponent(out PlayerAvatar avatar))
        {
            avatar.SetRole(role);
        }
    }

    public void SpawnChestForGuardian(ulong senderClientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer) return;

        if (senderClientId != GuardianClientId)
        {
            Debug.Log($"[Lab6] Solicitud de cofre rechazada: el cliente {senderClientId} no es el Guardian");
            return;
        }

        if (HasChest())
        {
            Debug.Log("[Lab6] Solicitud de cofre ignorada: ya existe un cofre");
            return;
        }

        if (chestPrefab == null)
        {
            Debug.LogError("[Lab6] chestPrefab no esta asignado: no se puede crear el cofre");
            return;
        }

        Vector3 spawnPosition = chestSpawnPosition;
        if (networkManager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient guardianClient) && guardianClient.PlayerObject != null)
        {
            Transform guardian = guardianClient.PlayerObject.transform;
            Vector3 forward = guardian.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }

            spawnPosition = guardian.position + forward.normalized * 2.6f;
            spawnPosition.y = chestSpawnPosition.y;
            spawnPosition.x = Mathf.Clamp(spawnPosition.x, -7.5f, 7.5f);
            spawnPosition.z = Mathf.Clamp(spawnPosition.z, -4f, 11f);
        }

        if (m_platform != null)
        {
            m_platform.transform.position = new Vector3(spawnPosition.x, -0.62f, spawnPosition.z);
        }

        if (m_chestLight != null)
        {
            m_chestLight.transform.position = spawnPosition + new Vector3(0f, 2.6f, 0f);
        }

        m_chestInstance = Instantiate(chestPrefab, spawnPosition, Quaternion.identity);
        m_chestInstance.Spawn();
        Debug.Log($"[Lab6] Cofre creado delante del Guardian (cliente {senderClientId}) en {spawnPosition}");
        Broadcast("Cofre invocado por el Guardian", Lab6MsgKind.Guardian);
    }

    public bool HasChest()
    {
        return m_chestInstance != null && m_chestInstance.IsSpawned;
    }

    private void ScanLoot(out int own, out int free)
    {
        own = 0;
        free = 0;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.SpawnManager == null) return;

        foreach (NetworkObject networkObject in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!networkObject.TryGetComponent(out ColorCube cube)) continue;
            if (cube.IsFreeLoot.Value)
            {
                free++;
            }
            else if (cube.IsOwner)
            {
                own++;
            }
        }
    }

    public static void PostMessage(string text, Lab6MsgKind kind = Lab6MsgKind.Info)
    {
        if (string.IsNullOrEmpty(text)) return;

        s_messages.Add(new HudMessage
        {
            Text = text,
            Color = KindColor(kind),
            Expiry = Time.unscaledTime + MessageLifetime
        });

        while (s_messages.Count > MaxMessages)
        {
            s_messages.RemoveAt(0);
        }
    }

    public static void Broadcast(string text, Lab6MsgKind kind = Lab6MsgKind.Info)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || networkManager.ShutdownInProgress || networkManager.SpawnManager == null) return;

        foreach (NetworkObject networkObject in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject.TryGetComponent(out PlayerAvatar avatar))
            {
                avatar.NotifyRpc(text, (byte)kind);
                return;
            }
        }
    }

    public static Color RoleColor(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Guardian: return GuardianColor;
            case PlayerRole.Explorer: return ExplorerColor;
            default: return new Color(0.75f, 0.75f, 0.75f);
        }
    }

    public static Color OwnerColor(ulong clientId)
    {
        switch (clientId % 6)
        {
            case 0: return new Color(1f, 0.72f, 0.18f);
            case 1: return new Color(0.25f, 0.80f, 1f);
            case 2: return new Color(1f, 0.40f, 0.78f);
            case 3: return new Color(0.40f, 1f, 0.45f);
            case 4: return new Color(1f, 0.48f, 0.28f);
            default: return new Color(0.80f, 0.80f, 1f);
        }
    }

    public static void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        Material material = renderer.material;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    private static Color KindColor(Lab6MsgKind kind)
    {
        switch (kind)
        {
            case Lab6MsgKind.Success: return new Color(0.45f, 1f, 0.5f);
            case Lab6MsgKind.Warning: return new Color(1f, 0.8f, 0.25f);
            case Lab6MsgKind.Danger: return new Color(1f, 0.35f, 0.35f);
            case Lab6MsgKind.Guardian: return GuardianColor;
            case Lab6MsgKind.Explorer: return ExplorerColor;
            default: return Color.white;
        }
    }

    private static string RoleText(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Guardian: return "GUARDIÁN";
            case PlayerRole.Explorer: return "EXPLORADOR";
            default: return "(sin jugador)";
        }
    }

    private static ulong FindGuardianId(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.SpawnManager == null) return ulong.MaxValue;

        foreach (NetworkObject networkObject in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject != null && networkObject.TryGetComponent(out PlayerAvatar avatar) &&
                avatar.Role.Value == PlayerRole.Guardian)
            {
                return networkObject.OwnerClientId;
            }
        }

        return ulong.MaxValue;
    }

    private void BuildEnvironment()
    {
        if (m_environmentBuilt) return;
        m_environmentBuilt = true;

        GameObject plane = GameObject.Find("Plane");
        Renderer planeRenderer = plane != null ? plane.GetComponent<Renderer>() : null;
        Material template = planeRenderer != null ? planeRenderer.sharedMaterial : null;

        if (planeRenderer != null)
        {
            SetRendererColor(planeRenderer, new Color(0.16f, 0.34f, 0.24f));
        }

        m_platform = CreateDecoration("Lab6Platform", PrimitiveType.Cylinder, new Vector3(chestSpawnPosition.x, -0.62f, chestSpawnPosition.z),
            new Vector3(2.4f, 0.08f, 2.4f), new Color(0.55f, 0.42f, 0.15f), template);
        CreateDecoration("Lab6PillarA", PrimitiveType.Cube, new Vector3(-9f, 1.4f, 8.5f), new Vector3(1.2f, 4.4f, 1.2f), new Color(0.30f, 0.28f, 0.26f), template);
        CreateDecoration("Lab6PillarB", PrimitiveType.Cube, new Vector3(9f, 1.4f, 8.5f), new Vector3(1.2f, 4.4f, 1.2f), new Color(0.30f, 0.28f, 0.26f), template);
        CreateDecoration("Lab6PillarC", PrimitiveType.Cube, new Vector3(-9f, 1.4f, -4.5f), new Vector3(1.2f, 4.4f, 1.2f), new Color(0.30f, 0.28f, 0.26f), template);
        CreateDecoration("Lab6PillarD", PrimitiveType.Cube, new Vector3(9f, 1.4f, -4.5f), new Vector3(1.2f, 4.4f, 1.2f), new Color(0.30f, 0.28f, 0.26f), template);

        GameObject lightObject = new GameObject("Lab6ChestLight");
        lightObject.transform.position = chestSpawnPosition + new Vector3(0f, 2.6f, 0f);
        m_chestLight = lightObject.AddComponent<Light>();
        m_chestLight.type = LightType.Point;
        m_chestLight.color = new Color(1f, 0.72f, 0.35f);
        m_chestLight.range = 9f;
        m_chestLight.intensity = 3.2f;
    }

    private GameObject CreateDecoration(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Material template)
    {
        GameObject decoration = GameObject.CreatePrimitive(type);
        decoration.name = name;
        decoration.transform.position = position;
        decoration.transform.localScale = scale;

        Collider collider = decoration.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Renderer renderer = decoration.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (template != null)
            {
                renderer.material = new Material(template);
            }
            SetRendererColor(renderer, color);
        }

        return decoration;
    }

    private void OnGUI()
    {
        if (!showHud) return;

        Initialize();
        EnsureStyles();
        PruneMessages();

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null) return;

        bool listening = networkManager.IsListening;
        PlayerRole localRole = PlayerRole.None;

        if (listening && networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null &&
            networkManager.LocalClient.PlayerObject.TryGetComponent(out PlayerAvatar avatar))
        {
            localRole = avatar.Role.Value;
        }

        if (!listening)
        {
            DrawStartPanel(networkManager);
        }
        else
        {
            DrawCubeLabels(networkManager);
            DrawHud(networkManager, localRole);
        }

        DrawMessages();
    }

    private void DrawStartPanel(NetworkManager networkManager)
    {
        GUILayout.BeginArea(new Rect(12f, 12f, 380f, 230f), s_panelStyle);
        GUI.color = GuardianColor;
        GUILayout.Label("EL COFRE DEL GUARDIÁN", s_titleStyle);
        GUI.color = Color.white;
        GUILayout.Label("Sin conexión. Inicia una instancia:", s_labelStyle);
        GUILayout.Space(6f);

        if (GUILayout.Button("Start Host  (H)"))
        {
            networkManager.StartHost();
        }
        if (GUILayout.Button("Start Client  (C)"))
        {
            networkManager.StartClient();
        }
        if (GUILayout.Button("Start Server"))
        {
            networkManager.StartServer();
        }

        GUILayout.Space(6f);
        GUILayout.Label("2 jugadores: Multiplayer Play Mode (Host en un editor, Client en el otro).", s_smallStyle);
        GUILayout.EndArea();
    }

    private void DrawHud(NetworkManager networkManager, PlayerRole localRole)
    {
        GUILayout.BeginArea(new Rect(12f, 12f, 400f, 330f), s_panelStyle);

        GUI.color = GuardianColor;
        GUILayout.Label("EL COFRE DEL GUARDIÁN", s_titleStyle);
        GUI.color = Color.white;

        string mode = networkManager.IsHost ? "HOST" : networkManager.IsServer ? "SERVIDOR" : "CLIENTE";
        string connection = networkManager.IsServer ? "ACTIVO" : networkManager.IsConnectedClient ? "CONECTADO" : "CONECTANDO...";
        GUILayout.Label($"Conexión: {mode} — {connection}   (id {networkManager.LocalClientId})", s_labelStyle);

        GUI.color = RoleColor(localRole);
        GUILayout.Label($"Rol: {RoleText(localRole)}", s_roleStyle);
        GUI.color = Color.white;

        GUILayout.Label($"Cubos propios: {m_ownLootCount}    Cubos libres en el suelo: {m_freeLootCount}", s_labelStyle);

        ulong guardianId = FindGuardianId(networkManager);
        if (guardianId != ulong.MaxValue)
        {
            GUILayout.Label($"Guardián actual: J{guardianId}", s_smallStyle);
        }

        GUILayout.Space(6f);
        GUILayout.Label("CONTROLES", s_smallStyle);
        GUILayout.Label("WASD: mover", s_smallStyle);
        GUILayout.Label("ESPACIO: interactuar / abrir cofre / recoger / reclamar", s_smallStyle);
        GUILayout.Label("G: pedir cofre (solo Guardián)", s_smallStyle);
        GUILayout.Label("E o clic derecho: lanzar cubo", s_smallStyle);
        GUILayout.Label("T: regalar cubo al jugador más cercano", s_smallStyle);
        GUILayout.Label("Q: consumir cubo", s_smallStyle);
        GUILayout.Label("X: desconectarse", s_smallStyle);

        GUILayout.Space(4f);
        if (GUILayout.Button("Shutdown  (X)"))
        {
            networkManager.Shutdown();
        }

        GUILayout.EndArea();
    }

    private void DrawCubeLabels(NetworkManager networkManager)
    {
        Camera camera = Camera.main;
        if (camera == null || networkManager.SpawnManager == null) return;

        foreach (NetworkObject networkObject in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!networkObject.TryGetComponent(out ColorCube cube)) continue;

            Vector3 screen = camera.WorldToScreenPoint(cube.transform.position + Vector3.up * 0.9f);
            if (screen.z <= 0f) continue;

            string text = cube.IsFreeLoot.Value ? "LIBRE" : $"J{cube.OwnerClientId}";
            Color color = cube.IsFreeLoot.Value ? FreeLootColor : OwnerColor(cube.OwnerClientId);
            Rect rect = new Rect(screen.x - 45f, Screen.height - screen.y - 10f, 90f, 18f);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Label(rect, text, s_centerStyle);
        }

        GUI.color = Color.white;
    }

    private void DrawMessages()
    {
        int count = s_messages.Count;
        for (int i = 0; i < count; i++)
        {
            HudMessage message = s_messages[i];
            float remaining = message.Expiry - Time.unscaledTime;
            float alpha = Mathf.Clamp01(remaining / 0.8f);

            Rect rect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 44f - (count - 1 - i) * 26f, 360f, 24f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(message.Color.r, message.Color.g, message.Color.b, alpha);
            GUI.Label(rect, message.Text, s_messageStyle);
        }

        GUI.color = Color.white;
    }

    private static void PruneMessages()
    {
        float now = Time.unscaledTime;
        for (int i = s_messages.Count - 1; i >= 0; i--)
        {
            if (s_messages[i].Expiry <= now)
            {
                s_messages.RemoveAt(i);
            }
        }
    }

    private static void EnsureStyles()
    {
        if (s_titleStyle != null && s_panelTexture != null) return;

        s_panelTexture = new Texture2D(2, 2);
        s_panelTexture.SetPixels(new[] { new Color(0.03f, 0.04f, 0.06f, 0.88f), new Color(0.03f, 0.04f, 0.06f, 0.88f), new Color(0.03f, 0.04f, 0.06f, 0.88f), new Color(0.03f, 0.04f, 0.06f, 0.88f) });
        s_panelTexture.Apply();

        s_panelStyle = new GUIStyle(GUI.skin.box);
        s_panelStyle.normal.background = s_panelTexture;
        s_panelStyle.padding = new RectOffset(12, 12, 10, 10);

        s_titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        s_labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        s_roleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
        s_smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        s_centerStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        s_messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        s_titleStyle.normal.textColor = Color.white;
        s_labelStyle.normal.textColor = Color.white;
        s_roleStyle.normal.textColor = Color.white;
        s_smallStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        s_centerStyle.normal.textColor = Color.white;
        s_messageStyle.normal.textColor = Color.white;
    }
}
