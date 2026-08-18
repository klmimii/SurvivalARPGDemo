using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 网络资源节点。服务器拥有可采集状态、奖励和刷新计时权。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkGatherableNode :
    NetworkBehaviour,
    IInteractable
{
    [Header("Server Reward")]
    [SerializeField]
    private ItemDefinition rewardItem;

    [Min(1)]
    [SerializeField]
    private int rewardAmount = 2;

    [Header("Server Validation")]
    [Min(0.5f)]
    [SerializeField]
    private float maximumInteractDistance = 2.6f;

    [Header("Refresh")]
    [Min(0.1f)]
    [SerializeField]
    private float refreshSeconds = 20f;

    [SerializeField]
    private GameObject visualRoot;

    [SerializeField]
    private Collider interactionCollider;

    private readonly NetworkVariable<bool> available = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Coroutine refreshRoutine;

    public bool IsAvailable => available.Value;

    public override void OnNetworkSpawn()
    {
        available.OnValueChanged += OnAvailableChanged;

        // 晚加入时 OnValueChanged 不一定再次触发，必须主动应用当前值。
        ApplyAvailableState(available.Value);
    }

    public override void OnNetworkDespawn()
    {
        available.OnValueChanged -= OnAvailableChanged;

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }
    }

    public string GetPromptText()
    {
        if (!available.Value)
        {
            return "资源正在恢复";
        }

        if (rewardItem == null)
        {
            return "资源配置错误";
        }

        return $"按E采集{rewardItem.displayName}x{rewardAmount}";
    }

    public bool TryInteract(GameObject interactor)
    {
        if (!IsSpawned || !available.Value || interactor == null)
        {
            return false;
        }

        NetworkObject playerObject =
            interactor.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsOwner)
        {
            return false;
        }

        RequestGatherServerRpc();
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestGatherServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (!available.Value || rewardItem == null || rewardAmount <= 0)
        {
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;

        NetworkObject playerObject =
            NetworkManager.SpawnManager
                .GetPlayerNetworkObject(senderClientId);

        if (playerObject == null)
        {
            return;
        }

        float distanceSqr =
            (playerObject.transform.position - transform.position)
            .sqrMagnitude;

        float allowedDistanceSqr =
            maximumInteractDistance * maximumInteractDistance;

        if (distanceSqr > allowedDistanceSqr)
        {
            Debug.LogWarning(
                $"拒绝远距离采集：ClientId={senderClientId}",
                this);
            return;
        }

        NetworkPlayerInventory inventory =
            playerObject.GetComponent<NetworkPlayerInventory>();

        if (inventory == null ||
            !inventory.ServerTryAdd(rewardItem, rewardAmount))
        {
            return;
        }

        // ServerRpc 会在服务器主线程顺序执行。
        // 第一个请求先把 available 改成 false，后续请求就会被拒绝。
        available.Value = false;

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
        }

        refreshRoutine = StartCoroutine(ServerRefreshRoutine());
    }

    private IEnumerator ServerRefreshRoutine()
    {
        yield return new WaitForSeconds(refreshSeconds);

        if (IsServer && IsSpawned)
        {
            available.Value = true;
        }

        refreshRoutine = null;
    }

    private void OnAvailableChanged(bool previous, bool current)
    {
        ApplyAvailableState(current);
    }

    private void ApplyAvailableState(bool value)
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(value);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = value;
        }
    }
}