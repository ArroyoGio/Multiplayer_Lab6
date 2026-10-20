using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAvatar : NetworkBehaviour
{
    public InputPlayer inputs;
    public float Speed;

    // Requisito 3: rol asignado por el servidor cuando aparece el jugador.
    public NetworkVariable<PlayerRole> Role = new NetworkVariable<PlayerRole>(PlayerRole.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector2 moveInput;

    private Chest currentChest;
    private ColorCube currentCube;
    private ColorCube currentFreeCube;
    private const float InteractionDistance = 5f;
    private const float GrabDistance = 2f;

    private void Awake()
    {
        inputs = new();
        inputs.Enable();
    }

    private void OnEnable()
    {
        inputs.Enable();

        inputs.Player.Move.performed += OnMovePerformed;
        inputs.Player.Move.canceled += OnMoveCanceled;
        inputs.Player.Interact.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        inputs.Player.Move.performed -= OnMovePerformed;
        inputs.Player.Move.canceled -= OnMoveCanceled;
        inputs.Player.Interact.performed -= OnInteractPerformed;

        inputs.Disable();
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        TryInteract();
    }

    public override void OnNetworkSpawn()
    {
        Role.OnValueChanged += OnRoleChanged;

        if (IsServer)
        {
            PlayerRole role = Lab6RoleManager.Instance != null ? Lab6RoleManager.Instance.ResolveRole(OwnerClientId) : PlayerRole.None;
            SetRole(role);
        }

        ApplyRoleVisual(Role.Value);
    }

    public override void OnNetworkDespawn()
    {
        Role.OnValueChanged -= OnRoleChanged;
    }

    public void SetRole(PlayerRole role)
    {
        if (!IsServer) return;

        Role.Value = role;
    }

    private void OnRoleChanged(PlayerRole previous, PlayerRole current)
    {
        ApplyRoleVisual(current);
    }

    private void ApplyRoleVisual(PlayerRole role)
    {
        Lab6RoleManager.SetRendererColor(GetComponent<Renderer>(), Lab6RoleManager.RoleColor(role));
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void NotifyRpc(string text, byte kind)
    {
        Lab6RoleManager.PostMessage(text, (Lab6MsgKind)kind);
    }

    private void TryInteract()
    {
        if (currentCube != null && currentCube.IsOwner && currentCube.IsHeld)
        {
            currentCube.ThrowObj(transform.forward * 10f + Vector3.up * 5f);
            currentCube = null;
            return;
        }

        // Requisito 7: reclamar el botin que quedo libre al desconectarse su dueno.
        if (currentFreeCube != null && currentFreeCube.IsFreeLoot.Value)
        {
            currentFreeCube.ClaimRpc();
            Lab6RoleManager.PostMessage("Reclamando botín libre...", Lab6MsgKind.Info);
            return;
        }

        if (currentChest != null)
        {
            InteractWithChestRpc(currentChest.NetworkObjectId);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void InteractWithChestRpc(ulong chestId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        var chest = Chest.GetChestByNetworkId(chestId);
        if (chest == null) return;

        if (chest.UserOpening.Value == senderId)
        {
            Debug.Log("Intentando cancelar apertura");
            chest.CancelOpening(senderId);
        }
        else
        {
            Debug.Log("Intentando abrir cofre");
            chest.StartOpening(senderId);
        }
    }

    // Requisito 4: el Guardian solicita al servidor la creacion del cofre.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestChestRpc(RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (Lab6RoleManager.Instance == null) return;

        Lab6RoleManager.Instance.SpawnChestForGuardian(rpcParams.Receive.SenderClientId);
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector3 dir = new Vector3(moveInput.x, 0, moveInput.y);
        transform.position += dir * Speed * Time.deltaTime;

        UpdateCurrentChest();
        UpdateCurrentCube();

        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            if (Role.Value == PlayerRole.Guardian)
            {
                RequestChestRpc();
            }
            else
            {
                Lab6RoleManager.PostMessage("Solo el Guardián puede invocar el cofre", Lab6MsgKind.Warning);
            }
        }
    }

    private void UpdateCurrentChest()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, InteractionDistance);
        Chest closestChest = null;
        float closestDistance = InteractionDistance;

        foreach (var collider in colliders)
        {
            Chest chest = collider.GetComponent<Chest>();
            if (chest != null)
            {
                float distance = Vector3.Distance(transform.position, chest.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestChest = chest;
                }
            }
        }

        currentChest = closestChest;
    }

    private void UpdateCurrentCube()
    {
        // Se vuelve a buscar botin si el cubo sostenido desaparecio o si ya no nos pertenece
        // (por ejemplo, despues de una transferencia de propiedad).
        if (currentCube != null)
        {
            if (currentCube.NetworkObject.IsSpawned && currentCube.IsOwner && currentCube.IsHeld)
            {
                return;
            }

            currentCube = null;
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, GrabDistance);
        ColorCube closestCube = null;
        float closestDistance = GrabDistance;
        ColorCube closestFreeCube = null;
        float closestFreeDistance = GrabDistance;

        foreach (var collider in colliders)
        {
            if (collider.gameObject.TryGetComponent(out ColorCube cube))
            {
                float distance = Vector3.Distance(transform.position, cube.transform.position);

                // Requisito 7: botin libre (sin dueno) que se puede reclamar.
                if (cube.IsFreeLoot.Value && distance < closestFreeDistance)
                {
                    closestFreeDistance = distance;
                    closestFreeCube = cube;
                }

                if (!cube.IsFreeLoot.Value && cube.CanBeGrabbed && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestCube = cube;
                }
            }
        }

        currentFreeCube = closestFreeCube;

        if (closestCube != null && closestCube.IsOwner)
        {
            currentCube = closestCube;
            closestCube.Grab(NetworkObject);
            Lab6RoleManager.PostMessage("¡Botín obtenido!", Lab6MsgKind.Success);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractionDistance);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, GrabDistance);
    }
}
