using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// 挂在每个 NetworkPlayer 上。
/// Server 保存真实 InventoryModel；Owner 保存只供本机 UI 使用的镜像。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerInventory : NetworkBehaviour
{
    [Min(1)]
    [SerializeField] private int capacity = 12;

    private NetworkList<NetworkItemStackData> networkSlots;

    // 只在服务器上创建和修改。
    private InventoryModel serverModel;

    // 只在该玩家的拥有者进程上创建，供 UI 读取。
    private InventoryModel ownerViewModel;

    public InventoryModel OwnerViewModel => ownerViewModel;

    private void Awake()
    {
        networkSlots = new NetworkList<NetworkItemStackData>(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            ownerViewModel = new InventoryModel(capacity);
            networkSlots.OnListChanged += OnNetworkSlotsChanged;
            BindOwnerInventoryUi();
        }

        if (IsServer)
        {
            serverModel = new InventoryModel(capacity);
            CopyServerModelToNetworkList();
        }

        if (IsOwner)
        {
            RebuildOwnerViewModel();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (networkSlots != null)
        {
            networkSlots.OnListChanged -= OnNetworkSlotsChanged;
        }

        serverModel = null;
        ownerViewModel = null;
    }

    private void OnDestroy()
    {
        if (networkSlots != null)
        {
            networkSlots.Dispose();
        }
    }

    /// <summary>
    /// 下一册的资源节点会在服务器上调用它。
    /// </summary>
    public bool ServerTryAdd(
        ItemDefinition definition,
        int amount)
    {
        if (!IsServer ||
            serverModel == null ||
            definition == null ||
            amount <= 0)
        {
            return false;
        }

        bool success = serverModel.TryAdd(definition, amount);
        if (!success)
        {
            return false;
        }

        CopyServerModelToNetworkList();
        return true;
    }

    public bool ServerTryRemove(
        ItemDefinition definition,
        int amount)
    {
        if (!IsServer ||
            serverModel == null ||
            definition == null ||
            amount <= 0)
        {
            return false;
        }

        bool success = serverModel.TryRemove(definition, amount);
        if (!success)
        {
            return false;
        }

        CopyServerModelToNetworkList();
        return true;
    }

    public int ServerCount(ItemDefinition definition)
    {
        if (!IsServer || serverModel == null || definition == null)
        {
            return 0;
        }

        return serverModel.CountItem(definition);
    }

    /// <summary>
    /// 本册 F6 测试使用。正式采集不会让客户端指定任意奖励，
    /// 而是由服务器资源节点读取自己的 rewardItem。
    /// </summary>
    public void RequestDebugAdd(
        ItemDefinition definition,
        int amount)
    {
        if (!IsOwner ||
            !IsSpawned ||
            definition == null ||
            string.IsNullOrWhiteSpace(definition.itemId) ||
            amount <= 0)
        {
            return;
        }

        RequestDebugAddServerRpc(
            new FixedString64Bytes(definition.itemId),
            Mathf.Clamp(amount, 1, 999));
    }

    [ServerRpc]
    private void RequestDebugAddServerRpc(
        FixedString64Bytes itemId,
        int amount)
    {
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError(
                "服务器找不到 ItemDatabase。",
                this);
            return;
        }

        ItemDefinition definition =
            ItemDatabase.Instance.GetById(itemId.ToString());

        if (definition == null)
        {
            Debug.LogWarning(
                $"服务器找不到物品 ID：{itemId}",
                this);
            return;
        }

        ServerTryAdd(definition, Mathf.Clamp(amount, 1, 999));
    }

    private void CopyServerModelToNetworkList()
    {
        if (!IsServer || serverModel == null)
        {
            return;
        }

        // 第一次建立固定数量的网络格子。
        if (networkSlots.Count != capacity)
        {
            networkSlots.Clear();

            for (int i = 0; i < capacity; i++)
            {
                networkSlots.Add(default);
            }
        }

        for (int i = 0; i < capacity; i++)
        {
            ItemStack slot = serverModel.Slots[i];

            if (slot.IsEmpty)
            {
                networkSlots[i] = default;
            }
            else
            {
                networkSlots[i] = new NetworkItemStackData(
                    slot.Definition.itemId,
                    slot.Amount);
            }
        }
    }

    private void OnNetworkSlotsChanged(
        NetworkListEvent<NetworkItemStackData> changeEvent)
    {
        RebuildOwnerViewModel();
    }

    private void RebuildOwnerViewModel()
    {
        if (!IsOwner || ownerViewModel == null)
        {
            return;
        }

        ItemDatabase database = ItemDatabase.Instance;
        if (database == null)
        {
            Debug.LogError(
                "本机找不到 ItemDatabase，无法还原背包物品 ID。",
                this);
            return;
        }

        for (int i = 0; i < capacity; i++)
        {
            if (i >= networkSlots.Count ||
                networkSlots[i].IsEmpty)
            {
                ownerViewModel.SetSlot(i, null, 0);
                continue;
            }

            NetworkItemStackData data = networkSlots[i];
            ItemDefinition definition =
                database.GetById(data.ItemId.ToString());

            if (definition == null)
            {
                Debug.LogWarning(
                    $"本机 ItemDatabase 缺少 ID：{data.ItemId}",
                    this);
                ownerViewModel.SetSlot(i, null, 0);
                continue;
            }

            ownerViewModel.SetSlot(
                i,
                definition,
                data.Amount);
        }

        // 所有格子写完后只广播一次，避免 UI 重复刷新。
        ownerViewModel.NotifyChanged();
    }

    private void BindOwnerInventoryUi()
    {
        InventoryPresenter presenter =
            Object.FindObjectOfType<InventoryPresenter>(true);

        if (presenter == null)
        {
            Debug.LogWarning(
                "NetworkPlayerInventory 找不到 InventoryPresenter。",
                this);
            return;
        }

        presenter.Bind(ownerViewModel);
    }

    public void ServerCaptureSlots(List<InventorySlotSaveData> destination)
    {
        if (!IsServer || serverModel == null || destination == null)
        {
            return;
        }

        destination.Clear();

        foreach (ItemStack slot in serverModel.Slots)
        {
            destination.Add(new InventorySlotSaveData
            {
                itemId = slot.IsEmpty
                    ? string.Empty
                    : slot.Definition.itemId,
                amount = slot.IsEmpty ? 0 : slot.Amount
            });
        }
    }

    public bool ServerRestoreSlots(
        IReadOnlyList<InventorySlotSaveData> savedSlots)
    {
        if (!IsServer || serverModel == null || savedSlots == null ||
            ItemDatabase.Instance == null)
        {
            return false;
        }

        for (int i = 0; i < serverModel.Capacity; i++)
        {
            if (i >= savedSlots.Count || savedSlots[i] == null ||
                string.IsNullOrWhiteSpace(savedSlots[i].itemId) ||
                savedSlots[i].amount <= 0)
            {
                serverModel.SetSlot(i, null, 0);
                continue;
            }

            ItemDefinition definition = ItemDatabase.Instance.GetById(
                savedSlots[i].itemId);

            if (definition == null)
            {
                Debug.LogWarning(
                    $"存档物品 ID 已失效：{savedSlots[i].itemId}",
                    this);
                serverModel.SetSlot(i, null, 0);
                continue;
            }

            serverModel.SetSlot(i, definition, savedSlots[i].amount);
        }

        serverModel.NotifyChanged();
        CopyServerModelToNetworkList();
        return true;
    }
}