using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 服务器生成和销毁的网络掉落物。
/// 网络只传 itemId 与数量，不传 ScriptableObject 引用。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPickupItem :
    NetworkBehaviour,
    IInteractable
{
    [Header("Optional Scene Default")]
    [Tooltip("只有手动摆在场景中的掉落物才需要填写。动态掉落由服务器初始化。")]
    [SerializeField]
    private ItemDefinition sceneItem;

    [Min(1)]
    [SerializeField]
    private int sceneAmount = 1;

    [Header("Server Validation")]
    [Min(0.5f)]
    [SerializeField]
    private float maximumInteractDistance = 2.6f;

    private readonly NetworkVariable<FixedString64Bytes> itemId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> amount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private bool claimed;

    public override void OnNetworkSpawn()
    {
        // 支持直接摆在网络场景中的测试掉落物。
        if (IsServer && itemId.Value.Length == 0 && sceneItem != null)
        {
            ServerInitialize(sceneItem, sceneAmount);
        }
    }

    /// <summary>
    /// 只能由服务器在 NetworkObject.Spawn() 前调用。
    /// </summary>
    public void ServerInitialize(
        ItemDefinition definition,
        int itemAmount)
    {
        // 这个方法会在 NetworkObject.Spawn() 前调用，
        // 因此使用 NetworkManager 判断当前进程是不是服务器。
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer ||
            definition == null ||
            string.IsNullOrWhiteSpace(definition.itemId))
        {
            return;
        }

        itemId.Value = new FixedString64Bytes(definition.itemId);
        amount.Value = Mathf.Max(1, itemAmount);
        claimed = false;
    }

    public string GetPromptText()
    {
        ItemDefinition definition = ResolveDefinition();

        if (definition == null)
        {
            return "按E拾取未知物品";
        }

        return $"按E拾取{definition.displayName}x{amount.Value}";
    }

    public bool TryInteract(GameObject interactor)
    {
        if (!IsSpawned || interactor == null)
        {
            return false;
        }

        NetworkObject playerObject =
            interactor.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsOwner)
        {
            return false;
        }

        RequestPickupServerRpc();
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (claimed || itemId.Value.Length == 0 || amount.Value <= 0)
        {
            return;
        }

        NetworkObject playerObject =
            NetworkManager.SpawnManager.GetPlayerNetworkObject(
                rpcParams.Receive.SenderClientId);

        if (playerObject == null)
        {
            return;
        }

        if ((playerObject.transform.position - transform.position)
            .sqrMagnitude >
            maximumInteractDistance * maximumInteractDistance)
        {
            return;
        }

        ItemDefinition definition = ResolveDefinition();
        NetworkPlayerInventory inventory =
            playerObject.GetComponent<NetworkPlayerInventory>();

        if (definition == null || inventory == null ||
            !inventory.ServerTryAdd(definition, amount.Value))
        {
            return;
        }

        NetworkQuestService questService =
    playerObject.GetComponent<NetworkQuestService>();

        questService?.ServerAddProgress(
            QuestObjectiveType.ObtainItem,
            definition.itemId,
            amount.Value);

        claimed = true;
        NetworkObject.Despawn(true);
    }

    private ItemDefinition ResolveDefinition()
    {
        if (itemId.Value.Length == 0 || ItemDatabase.Instance == null)
        {
            return null;
        }

        return ItemDatabase.Instance.GetById(itemId.Value.ToString());
    }
}