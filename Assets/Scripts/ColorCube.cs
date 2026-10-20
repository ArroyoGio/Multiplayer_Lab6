using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.InputSystem;

public class ColorCube : NetworkBehaviour
{
    public Vector3 HandSlot = new Vector3(0.6f, 0f, 0.65f);

    // Requisito 7: botin libre cuando su dueno se desconecto (persiste en el mundo).
    public NetworkVariable<bool> IsFreeLoot = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector3 spawnPoint;
    private bool isHeld = false;
    private float m_grabBlockedUntil;

    public bool IsHeld => isHeld;
    public bool CanBeGrabbed => Time.time >= m_grabBlockedUntil;

    public override void OnNetworkSpawn()
    {
        spawnPoint = transform.position;
        IsFreeLoot.OnValueChanged += OnFreeLootChanged;
        RefreshColor();
    }

    public override void OnNetworkDespawn()
    {
        IsFreeLoot.OnValueChanged -= OnFreeLootChanged;
    }

    private void OnFreeLootChanged(bool previous, bool current)
    {
        RefreshColor();
    }

    private Color CurrentColor()
    {
        return IsFreeLoot.Value ? Lab6RoleManager.FreeLootColor : Lab6RoleManager.OwnerColor(OwnerClientId);
    }

    private void RefreshColor()
    {
        Lab6RoleManager.SetRendererColor(GetComponent<Renderer>(), CurrentColor());
    }

    // Requisito 7: al desconectarse el dueno, el cubo queda en el mundo como botin libre.
    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        base.OnOwnershipChanged(previous, current);

        isHeld = false;
        RefreshColor();

        if (previous != current && isActiveAndEnabled)
        {
            StartCoroutine(Flash());
        }

        if (!IsServer) return;

        if (current == NetworkManager.ServerClientId && previous != NetworkManager.ServerClientId)
        {
            IsFreeLoot.Value = true;

            var freeLootBody = GetComponent<NetworkRigidbody>().Rigidbody;
            freeLootBody.isKinematic = false;
            freeLootBody.useGravity = true;
        }
        else if (current != NetworkManager.ServerClientId)
        {
            IsFreeLoot.Value = false;
        }
    }

    private IEnumerator Flash()
    {
        Lab6RoleManager.SetRendererColor(GetComponent<Renderer>(), Color.white);
        yield return new WaitForSeconds(0.12f);
        RefreshColor();
    }

    public void Grab(NetworkObject user)
    {
        isHeld = true;
        GetComponent<NetworkRigidbody>().Rigidbody.isKinematic = true;
        GetComponent<NetworkRigidbody>().Rigidbody.useGravity = false;
        NetworkObject.TrySetParent(user, false);
        transform.localPosition = HandSlot;
        transform.localRotation = Quaternion.identity;
    }

    public void ThrowObj(Vector3 throwSpeed)
    {
        isHeld = false;
        m_grabBlockedUntil = Time.time + 0.6f;
        NetworkObject.TryRemoveParent(true);

        GetComponent<NetworkRigidbody>().Rigidbody.isKinematic = false;
        GetComponent<NetworkRigidbody>().Rigidbody.useGravity = true;
        GetComponent<NetworkRigidbody>().SetLinearVelocity(throwSpeed);
    }

    public void ReleaseObj()
    {
        if (isHeld)
        {
            ThrowObj(Vector3.zero);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isHeld && collision.gameObject.TryGetComponent(out PlayerAvatar _))
        {
            ReleaseObj();
        }
    }

    private void LateUpdate()
    {
        if (!isHeld) return;
        if (!IsOwner) return;

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        // Se conserva el lanzamiento original, adaptado al Input System (el proyecto solo tiene el paquete nuevo).
        if ((mouse != null && mouse.rightButton.isPressed) || (keyboard != null && keyboard.eKey.isPressed))
        {
            ThrowObj(Camera.main.transform.forward * 10f + Vector3.up * 5f);
            return;
        }

        // Requisito 6: transferir el botin al jugador mas cercano.
        if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
        {
            TransferToNearestPlayerRpc();
            return;
        }

        // Requisito 7: consumir el botin.
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            isHeld = false;
            ConsumeRpc();
        }
    }

    // Requisito 6: transferencia de propiedad al jugador mas cercano, validada por el servidor.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void TransferToNearestPlayerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (NetworkObject.OwnerClientId != senderClientId) return;

        PlayerAvatar nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (NetworkObject networkObject in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!networkObject.TryGetComponent(out PlayerAvatar candidate)) continue;
            if (candidate.OwnerClientId == senderClientId) continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        if (nearest == null)
        {
            Lab6RoleManager.Broadcast("No hay otro jugador al que transferir el botín", Lab6MsgKind.Warning);
            return;
        }

        // El botin sale de la mano del dueno anterior para que lo pueda recoger el nuevo dueno.
        NetworkObject.TryRemoveParent(true);
        var transferBody = GetComponent<NetworkRigidbody>().Rigidbody;
        transferBody.isKinematic = false;
        transferBody.useGravity = true;

        NetworkObject.ChangeOwnership(nearest.OwnerClientId);
        Lab6RoleManager.Broadcast($"Cubo transferido al jugador {nearest.OwnerClientId}", Lab6MsgKind.Success);
        Debug.Log($"[Lab6] Botin {NetworkObjectId} transferido al cliente {nearest.OwnerClientId} (distancia {nearestDistance})");
    }

    // Requisito 7: el consumo del botin lo elimina del mundo.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void ConsumeRpc(RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (NetworkObject.OwnerClientId != rpcParams.Receive.SenderClientId) return;

        Lab6RoleManager.Broadcast($"Cubo consumido por el jugador {rpcParams.Receive.SenderClientId}", Lab6MsgKind.Warning);
        Debug.Log($"[Lab6] Botin {NetworkObjectId} consumido por el cliente {rpcParams.Receive.SenderClientId}");

        PlayConsumeFxRpc();

        if (isActiveAndEnabled)
        {
            StartCoroutine(DespawnAfter(0.3f));
        }
        else
        {
            NetworkObject.Despawn(true);
        }
    }

    private IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayConsumeFxRpc()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(ConsumeFx());
        }
    }

    private IEnumerator ConsumeFx()
    {
        Renderer cubeRenderer = GetComponent<Renderer>();
        if (cubeRenderer == null) yield break;

        Color start = CurrentColor();
        float elapsed = 0f;
        const float duration = 0.3f;

        while (elapsed < duration && cubeRenderer != null)
        {
            elapsed += Time.deltaTime;
            Lab6RoleManager.SetRendererColor(cubeRenderer, Color.Lerp(start, Color.black, elapsed / duration));
            yield return null;
        }
    }

    // Requisito 7: el botin libre (dueno desconectado) lo puede reclamar cualquier jugador conectado.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ClaimRpc(RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!IsFreeLoot.Value) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.ContainsKey(senderClientId)) return;

        NetworkObject.ChangeOwnership(senderClientId);
        IsFreeLoot.Value = false;
        Lab6RoleManager.Broadcast($"Botín libre reclamado por el jugador {senderClientId}", Lab6MsgKind.Success);
        Debug.Log($"[Lab6] Botin libre {NetworkObjectId} reclamado por el cliente {senderClientId}");
    }
}
