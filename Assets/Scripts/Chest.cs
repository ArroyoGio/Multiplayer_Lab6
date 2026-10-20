using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Chest : NetworkBehaviour
{
    public NetworkObject cubePrefab;
    public float TimeToOpenChest = 3f;

    public NetworkVariable<ulong> UserOpening = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<float> Progress = new NetworkVariable<float>();

    private static Dictionary<ulong, Chest> s_allChests = new Dictionary<ulong, Chest>();
    private static readonly Color WoodColor = new Color(0.45f, 0.24f, 0.08f);
    private static readonly Color WoodDark = new Color(0.28f, 0.14f, 0.05f);
    private static readonly Color GoldColor = new Color(1f, 0.78f, 0.22f);
    private bool m_visualsBuilt;

    private void Awake()
    {
        BuildVisuals();
    }

    public override void OnNetworkSpawn()
    {
        // Progress es de escritura exclusiva del servidor: en los clientes no se puede escribir.
        if (IsServer)
        {
            Progress.Value = 0f;
        }

        s_allChests[NetworkObjectId] = this;
    }

    public override void OnNetworkDespawn()
    {
        s_allChests.Remove(NetworkObjectId);

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && !networkManager.ShutdownInProgress)
        {
            SpawnOpenBurst();
        }
    }

    public static Chest GetChestByNetworkId(ulong networkId)
    {
        s_allChests.TryGetValue(networkId, out Chest chest);
        return chest;
    }

    private void BuildVisuals()
    {
        if (m_visualsBuilt) return;
        m_visualsBuilt = true;

        Renderer bodyRenderer = GetComponent<Renderer>();
        Material template = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;

        if (bodyRenderer != null)
        {
            Lab6RoleManager.SetRendererColor(bodyRenderer, WoodColor);
        }

        AddPart("Lid", new Vector3(0f, 0.53f, 0f), new Vector3(1.10f, 0.18f, 1.10f), WoodDark, template);
        AddPart("BandLeft", new Vector3(-0.33f, 0f, 0.51f), new Vector3(0.10f, 1.02f, 0.05f), GoldColor, template);
        AddPart("BandRight", new Vector3(0.33f, 0f, 0.51f), new Vector3(0.10f, 1.02f, 0.05f), GoldColor, template);
        AddPart("Lock", new Vector3(0f, 0.06f, 0.53f), new Vector3(0.22f, 0.24f, 0.08f), GoldColor, template);
        AddPart("FootBL", new Vector3(-0.40f, -0.53f, -0.40f), new Vector3(0.18f, 0.12f, 0.18f), WoodDark, template);
        AddPart("FootBR", new Vector3(0.40f, -0.53f, -0.40f), new Vector3(0.18f, 0.12f, 0.18f), WoodDark, template);
        AddPart("FootFL", new Vector3(-0.40f, -0.53f, 0.40f), new Vector3(0.18f, 0.12f, 0.18f), WoodDark, template);
        AddPart("FootFR", new Vector3(0.40f, -0.53f, 0.40f), new Vector3(0.18f, 0.12f, 0.18f), WoodDark, template);
    }

    private void AddPart(string partName, Vector3 localPosition, Vector3 localScale, Color color, Material template)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            partCollider.enabled = false;
            Destroy(partCollider);
        }

        Renderer partRenderer = part.GetComponent<Renderer>();
        if (partRenderer != null)
        {
            if (template != null)
            {
                partRenderer.material = new Material(template);
            }
            Lab6RoleManager.SetRendererColor(partRenderer, color);
        }
    }

    private void SpawnOpenBurst()
    {
        Renderer bodyRenderer = GetComponent<Renderer>();
        Material template = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;

        for (int i = 0; i < 10; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "ChestSpark";
            spark.transform.position = transform.position + Vector3.up * 0.35f;
            spark.transform.localScale = Vector3.one * Random.Range(0.10f, 0.18f);

            Collider sparkCollider = spark.GetComponent<Collider>();
            if (sparkCollider != null)
            {
                sparkCollider.enabled = false;
                Destroy(sparkCollider);
            }

            Renderer sparkRenderer = spark.GetComponent<Renderer>();
            if (sparkRenderer != null)
            {
                if (template != null)
                {
                    sparkRenderer.material = new Material(template);
                }
                Lab6RoleManager.SetRendererColor(sparkRenderer, GoldColor);
            }

            Rigidbody sparkBody = spark.AddComponent<Rigidbody>();
            Vector3 direction = Random.insideUnitSphere;
            direction.y = Mathf.Abs(direction.y) + 0.4f;
            sparkBody.AddForce(direction.normalized * Random.Range(2.5f, 4.5f), ForceMode.VelocityChange);

            Destroy(spark, 0.7f);
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (UserOpening.Value == ulong.MaxValue) return;

        Progress.Value += Time.deltaTime / Mathf.Max(TimeToOpenChest, 0.0001f);
        if (Progress.Value >= 1f)
        {
            OpenChest();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartOpeningChestRpc(RpcParams rpcParams = default)
    {
        StartOpening(rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CancelOpeningChestRpc(RpcParams rpcParams = default)
    {
        CancelOpening(rpcParams.Receive.SenderClientId);
    }

    public void StartOpening(ulong clientId)
    {
        if (!IsServer) return;

        if (Lab6RoleManager.Instance == null || Lab6RoleManager.Instance.GuardianClientId != clientId)
        {
            Lab6RoleManager.Broadcast("Solo el Guardián puede abrir el cofre", Lab6MsgKind.Warning);
            return;
        }

        if (UserOpening.Value != ulong.MaxValue && UserOpening.Value != clientId) return;
        UserOpening.Value = clientId;
    }

    public void CancelOpening(ulong clientId)
    {
        if (!IsServer) return;
        if (UserOpening.Value != clientId) return;
        UserOpening.Value = ulong.MaxValue;
        Progress.Value = 0f;
    }

    public void OpenChest()
    {
        // Requisito 5: cada cubo de botin nace con la propiedad de red del jugador que abrio el cofre.
        ulong ownerClientId = UserOpening.Value == ulong.MaxValue ? NetworkManager.ServerClientId : UserOpening.Value;

        for (int i = 0; i < 5; i++)
        {
            Vector3 spawnPoint = transform.position + Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(2f, 1f, 0f);
            NetworkObject colorCube = Instantiate(cubePrefab, spawnPoint, Random.rotation);
            colorCube.SpawnWithOwnership(ownerClientId);
        }

        Lab6RoleManager.Broadcast("¡El cofre se ha abierto!", Lab6MsgKind.Success);

        if (NetworkObject != null)
        {
            NetworkObject.Despawn();
        }
    }
}
