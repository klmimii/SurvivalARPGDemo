using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public sealed class NetworkBuildingEntry
{
    public BuildingDefinition definition;
    public NetworkPlacedBuilding networkPrefab;
}

/// <summary>
/// 挂在每个NetworkGameplayPlayer上。
/// ServerRpc由该玩家拥有者调用，服务器负责最终校验、付款和Spawn。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkBuildingService : NetworkBehaviour
{
    [Header("Catalog Whitelist")]
    [SerializeField]
    private NetworkBuildingEntry[] entries;

    [Header("Server Validation")]
    [Min(1f)]
    [SerializeField]
    private float maximumBuildDistance = 8f;

    [SerializeField]
    private LayerMask placementRayLayers;

    [SerializeField]
    private LayerMask blockingLayers;

    [Min(0.5f)]
    [SerializeField]
    private float groundCheckHeight = 2f;

    [Min(0.5f)]
    [SerializeField]
    private float groundCheckDistance = 4f;

    private ToastView ownerToast;

    /// <summary>
    /// 服务器把一次建造或拆除结果发回拥有者时触发。
    /// 正式操作控制器用它解除“正在等待服务器”的状态。
    /// </summary>
    public event Action<string> OwnerResultReceived;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            ownerToast = UnityEngine.Object.FindObjectOfType<ToastView>(true);
        }
    }

    public void RequestPlace(
        BuildingDefinition definition,
        Vector3 requestedPosition,
        Quaternion requestedRotation)
    {
        if (!IsOwner || !IsSpawned || definition == null ||
            string.IsNullOrWhiteSpace(definition.buildingId))
        {
            return;
        }

        RequestPlaceServerRpc(
            new FixedString64Bytes(definition.buildingId),
            requestedPosition,
            requestedRotation);
    }

    public void RequestDemolish(NetworkPlacedBuilding target)
    {
        if (!IsOwner || !IsSpawned || target == null ||
            !target.IsSpawned)
        {
            return;
        }

        RequestDemolishServerRpc(
            new NetworkObjectReference(target.NetworkObject));
    }

    [ServerRpc]
    private void RequestPlaceServerRpc(
        FixedString64Bytes buildingId,
        Vector3 requestedPosition,
        Quaternion requestedRotation,
        ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        NetworkObject playerObject =
            NetworkManager.SpawnManager.GetPlayerNetworkObject(senderId);

        NetworkBuildingEntry entry = FindEntry(buildingId.ToString());

        if (playerObject == null || entry == null ||
            entry.definition == null || entry.networkPrefab == null)
        {
            ServerSendResult(senderId, "服务器没有找到该建筑配置。");
            return;
        }

        if ((requestedPosition - playerObject.transform.position)
            .sqrMagnitude > maximumBuildDistance * maximumBuildDistance)
        {
            ServerSendResult(senderId, "放置位置离玩家太远。");
            return;
        }

        BuildingDefinition definition = entry.definition;

        // 服务器不接受任意倾斜，只保留最接近90度倍数的Y旋转。
        float safeYaw = Mathf.Round(
            requestedRotation.eulerAngles.y / 90f) * 90f;
        Quaternion safeRotation = Quaternion.Euler(0f, safeYaw, 0f);

        Vector3 safePosition = SnapPositionToGrid(
            requestedPosition,
            definition.gridSize);

        BuildingSocket selectedSocket = null;
        PlacedBuilding ignoredSupport = null;
        NetworkPlacedBuilding networkSupport = null;

        if (definition.allowSnapping)
        {
            selectedSocket = BuildingSocket.FindBest(
                definition.pieceType,
                safePosition,
                definition.snapSearchRadius);

            if (selectedSocket != null)
            {
                safePosition = selectedSocket.transform.position;
                safeRotation = selectedSocket.transform.rotation *
                    Quaternion.Euler(0f, safeYaw, 0f);
                ignoredSupport = selectedSocket.Owner;
                networkSupport = ignoredSupport != null
                    ? ignoredSupport.GetComponent<NetworkPlacedBuilding>()
                    : null;
            }
        }

        if (definition.requireSnap && selectedSocket == null)
        {
            ServerSendResult(senderId, "该建筑必须吸附到兼容建筑。");
            return;
        }

        // 找到兼容且未占用的Socket时，Socket本身就是合法支撑。
        // 只有自由放置时，才继续检查Ground/Floor表面。
        if (selectedSocket == null &&
            !ServerValidateSurface(
                definition,
                safePosition,
                ref ignoredSupport,
                ref networkSupport))
        {
            ServerSendResult(senderId, "建筑表面类型不允许。");
            return;
        }

        // 地板拼接时不能忽略相邻地板；墙和家具可以忽略支撑地板。
        PlacedBuilding validatorIgnoredSupport =
            definition.pieceType == BuildingPieceType.Floor
                ? null
                : ignoredSupport;

        if (!BuildingPlacementValidator.IsAreaFree(
                definition,
                safePosition,
                safeRotation,
                blockingLayers,
                validatorIgnoredSupport))
        {
            ServerSendResult(senderId, "该位置已被其他物体阻挡。");
            return;
        }

        NetworkPlayerInventory inventory =
            playerObject.GetComponent<NetworkPlayerInventory>();

        // 必须单独判空。若写成 inventory == null || ServerTryConsume...，
        // 短路求值会导致 paymentMessage 没有赋值，引发 CS0165。
        if (inventory == null)
        {
            ServerSendResult(senderId, "服务器找不到该玩家的网络背包。");
            return;
        }

        if (!ServerTryConsumeCost(
                inventory,
                definition,
                out BuildingPaymentSource payment,
                out string paymentMessage))
        {
            ServerSendResult(senderId, paymentMessage);
            return;
        }

        NetworkPlacedBuilding instance = Instantiate(
            entry.networkPrefab,
            safePosition,
            safeRotation);

        // 必须先Spawn，再写NetworkVariable。
        // 否则NGO会警告 NetworkVariable is written to, but NetworkBehaviour is not spawned。
        instance.NetworkObject.Spawn(true);
        instance.ServerInitialize(senderId, payment, networkSupport);

        PlacedBuilding placed = instance.GetComponent<PlacedBuilding>();
        bool socketReserved = selectedSocket == null ||
            (placed != null && selectedSocket.TryReserve(placed));

        if (!socketReserved)
        {
            instance.NetworkObject.Despawn(true);
            ServerTryRefundCost(inventory, definition, payment, true);
            ServerSendResult(senderId, "吸附点刚刚被占用，材料已回滚。");
            return;
        }

        NetworkQuestService questService =
    playerObject.GetComponent<NetworkQuestService>();

        questService?.ServerAddProgress(
            QuestObjectiveType.BuildBuilding,
            definition.buildingId,
            1);

        ServerSendResult(
            senderId,
            $"已建造{definition.displayName}。{paymentMessage}");
    }

    [ServerRpc]
    private void RequestDemolishServerRpc(
        NetworkObjectReference targetReference,
        ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!targetReference.TryGet(out NetworkObject targetObject) ||
            targetObject == null)
        {
            ServerSendResult(senderId, "目标建筑已经不存在。");
            return;
        }

        NetworkPlacedBuilding target =
            targetObject.GetComponent<NetworkPlacedBuilding>();
        NetworkObject playerObject =
            NetworkManager.SpawnManager.GetPlayerNetworkObject(senderId);

        if (target == null || playerObject == null ||
            target.Definition == null)
        {
            ServerSendResult(senderId, "目标不是可拆除的网络建筑。");
            return;
        }

        if (target.BuilderClientId != senderId)
        {
            ServerSendResult(senderId, "只能拆除自己建造的建筑。");
            return;
        }

        if ((target.transform.position - playerObject.transform.position)
            .sqrMagnitude > maximumBuildDistance * maximumBuildDistance)
        {
            ServerSendResult(senderId, "离建筑太远，不能拆除。");
            return;
        }

        if (ServerHasDependentBuilding(target))
        {
            ServerSendResult(senderId, "请先拆除依附在它上面的建筑。");
            return;
        }

        NetworkPlayerInventory inventory =
            playerObject.GetComponent<NetworkPlayerInventory>();

        if (inventory == null ||
            !ServerTryRefundCost(
                inventory,
                target.Definition,
                target.PaymentSource,
                false))
        {
            ServerSendResult(senderId, "背包空间不足，无法返还材料。");
            return;
        }

        string displayName = target.Definition.displayName;
        target.NetworkObject.Despawn(true);
        ServerSendResult(senderId, $"已拆除{displayName}并返还材料。");
    }

    private bool ServerValidateSurface(
        BuildingDefinition definition,
        Vector3 position,
        ref PlacedBuilding support,
        ref NetworkPlacedBuilding networkSupport)
    {
        if (!Physics.Raycast(
                position + Vector3.up * groundCheckHeight,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                placementRayLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        PlacedBuilding hitBuilding =
            hit.collider.GetComponentInParent<PlacedBuilding>();

        BuildSurfaceType surface = BuildSurfaceType.Ground;

        if (hitBuilding != null && hitBuilding.Definition != null &&
            hitBuilding.Definition.pieceType == BuildingPieceType.Floor)
        {
            surface = BuildSurfaceType.Floor;
            support = hitBuilding;
            networkSupport =
                hitBuilding.GetComponent<NetworkPlacedBuilding>();
        }

        return definition.AllowsSurface(surface) ||
            (definition.requireSnap && support != null);
    }

    private bool ServerTryConsumeCost(
        NetworkPlayerInventory inventory,
        BuildingDefinition definition,
        out BuildingPaymentSource source,
        out string message)
    {
        source = BuildingPaymentSource.Unknown;
        message = "材料不足。";

        // 1. 无论配方是否解锁，先用已经合成出的建筑成品。
        if (definition.requiredKit != null &&
            inventory.ServerCount(definition.requiredKit) > 0)
        {
            if (inventory.ServerTryRemove(definition.requiredKit, 1))
            {
                source = BuildingPaymentSource.BuildingItem;
                message = $"已使用{definition.requiredKit.displayName}。";
                return true;
            }
        }

        // 2. 建筑成品没有了，才走配方原料兜底。
        RecipeDefinition recipe = definition.craftingRecipe;
        if (definition.allowIngredientFallback && recipe != null)
        {
            if (recipe.unlockItem != null &&
                inventory.ServerCount(recipe.unlockItem) <= 0)
            {
                message = "尚未获得该建筑配方。";
                return false;
            }

            if (recipe.outputItem != definition.requiredKit ||
                recipe.outputAmount != 1)
            {
                message = "建筑配方产物配置不正确。";
                return false;
            }

            Dictionary<ItemDefinition, int> ingredients =
                BuildRecipeAmounts(recipe);

            if (ServerTryRemoveAll(inventory, ingredients))
            {
                source = BuildingPaymentSource.RecipeIngredients;
                message = "建筑成品不足，已直接消耗配方原料。";
                return true;
            }

            message = "建筑成品和配方原料都不足。";
            return false;
        }

        // 3. 兼容尚未迁移的旧 costs。
        Dictionary<ItemDefinition, int> legacy =
            BuildLegacyAmounts(definition, 1f);

        if (ServerTryRemoveAll(inventory, legacy))
        {
            source = BuildingPaymentSource.LegacyDirectIngredients;
            message = "已消耗旧版直接材料。";
            return true;
        }

        return false;
    }

    private bool ServerTryRefundCost(
        NetworkPlayerInventory inventory,
        BuildingDefinition definition,
        BuildingPaymentSource source,
        bool forceFullRefund)
    {
        float rate = forceFullRefund ? 1f : definition.refundRate;
        Dictionary<ItemDefinition, int> refund;

        switch (source)
        {
            case BuildingPaymentSource.BuildingItem:
                refund = new Dictionary<ItemDefinition, int>();
                int kitAmount = Mathf.FloorToInt(rate);
                if (definition.requiredKit != null && kitAmount > 0)
                {
                    refund[definition.requiredKit] = kitAmount;
                }
                break;

            case BuildingPaymentSource.RecipeIngredients:
                refund = BuildRecipeAmounts(
                    definition.craftingRecipe,
                    rate);
                break;

            case BuildingPaymentSource.LegacyDirectIngredients:
                refund = BuildLegacyAmounts(definition, rate);
                break;

            default:
                return false;
        }

        return ServerTryAddAll(inventory, refund);
    }

    private static bool ServerTryRemoveAll(
        NetworkPlayerInventory inventory,
        Dictionary<ItemDefinition, int> amounts)
    {
        if (amounts == null || amounts.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            if (pair.Key == null || pair.Value <= 0 ||
                inventory.ServerCount(pair.Key) < pair.Value)
            {
                return false;
            }
        }

        List<KeyValuePair<ItemDefinition, int>> removed =
            new List<KeyValuePair<ItemDefinition, int>>();

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            if (!inventory.ServerTryRemove(pair.Key, pair.Value))
            {
                foreach (KeyValuePair<ItemDefinition, int> rollback in removed)
                {
                    inventory.ServerTryAdd(rollback.Key, rollback.Value);
                }
                return false;
            }

            removed.Add(pair);
        }

        return true;
    }

    private static bool ServerTryAddAll(
        NetworkPlayerInventory inventory,
        Dictionary<ItemDefinition, int> amounts)
    {
        if (amounts == null || amounts.Count == 0)
        {
            return true;
        }

        List<KeyValuePair<ItemDefinition, int>> added =
            new List<KeyValuePair<ItemDefinition, int>>();

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            if (pair.Key == null || pair.Value <= 0)
            {
                continue;
            }

            if (!inventory.ServerTryAdd(pair.Key, pair.Value))
            {
                foreach (KeyValuePair<ItemDefinition, int> rollback in added)
                {
                    inventory.ServerTryRemove(rollback.Key, rollback.Value);
                }
                return false;
            }

            added.Add(pair);
        }

        return true;
    }

    private static Dictionary<ItemDefinition, int> BuildRecipeAmounts(
        RecipeDefinition recipe,
        float rate = 1f)
    {
        Dictionary<ItemDefinition, int> result =
            new Dictionary<ItemDefinition, int>();

        if (recipe == null || recipe.ingredients == null)
        {
            return result;
        }

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.item == null ||
                ingredient.amount <= 0)
            {
                continue;
            }

            int amount = Mathf.FloorToInt(ingredient.amount * rate);
            AddAmount(result, ingredient.item, amount);
        }

        return result;
    }

    private static Dictionary<ItemDefinition, int> BuildLegacyAmounts(
        BuildingDefinition definition,
        float rate)
    {
        Dictionary<ItemDefinition, int> result =
            new Dictionary<ItemDefinition, int>();

        if (definition == null || definition.costs == null)
        {
            return result;
        }

        foreach (BuildingCost cost in definition.costs)
        {
            if (cost == null || cost.item == null || cost.amount <= 0)
            {
                continue;
            }

            AddAmount(
                result,
                cost.item,
                Mathf.FloorToInt(cost.amount * rate));
        }

        return result;
    }

    private static void AddAmount(
        Dictionary<ItemDefinition, int> result,
        ItemDefinition item,
        int amount)
    {
        if (item == null || amount <= 0)
        {
            return;
        }

        result.TryGetValue(item, out int current);
        result[item] = current + amount;
    }

    /// <summary>
    /// 仅检查该建筑是否存在于服务器白名单。
    /// 这能避免菜单引用了一个服务器根本不认识的配置。
    /// </summary>
    public bool HasDefinition(BuildingDefinition definition)
    {
        return definition != null &&
            !string.IsNullOrWhiteSpace(definition.buildingId) &&
            FindEntry(definition.buildingId) != null;
    }

    /// <summary>
    /// 用拥有者本机的网络背包镜像估算能否支付。
    /// 只控制预览红绿；服务器仍会在 RequestPlaceServerRpc 中重新验证并扣费。
    /// </summary>
    public bool OwnerCanAfford(BuildingDefinition definition)
    {
        if (!IsOwner || definition == null)
        {
            return false;
        }

        NetworkPlayerInventory inventory =
            GetComponent<NetworkPlayerInventory>();
        InventoryModel model = inventory != null
            ? inventory.OwnerViewModel
            : null;

        if (model == null)
        {
            return false;
        }

        // 与服务器一致：先使用已经合成好的建筑成品。
        if (definition.requiredKit != null &&
            model.CountItem(definition.requiredKit) > 0)
        {
            return true;
        }

        // 没有成品时，检查配方解锁和全部原料。
        RecipeDefinition recipe = definition.craftingRecipe;
        if (definition.allowIngredientFallback && recipe != null)
        {
            if (recipe.unlockItem != null &&
                model.CountItem(recipe.unlockItem) <= 0)
            {
                return false;
            }

            if (recipe.outputItem != definition.requiredKit ||
                recipe.outputAmount != 1)
            {
                return false;
            }

            return OwnerHasAll(
                model,
                BuildRecipeAmounts(recipe));
        }

        // 兼容尚未迁移到 RecipeDefinition 的旧 costs。
        return OwnerHasAll(
            model,
            BuildLegacyAmounts(definition, 1f));
    }

    private static bool OwnerHasAll(
        InventoryModel model,
        Dictionary<ItemDefinition, int> amounts)
    {
        if (model == null || amounts == null || amounts.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            if (pair.Key == null || pair.Value <= 0 ||
                model.CountItem(pair.Key) < pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private NetworkBuildingEntry FindEntry(string buildingId)
    {
        if (entries == null)
        {
            return null;
        }

        foreach (NetworkBuildingEntry entry in entries)
        {
            if (entry != null && entry.definition != null &&
                entry.definition.buildingId == buildingId)
            {
                return entry;
            }
        }

        return null;
    }

    private static Vector3 SnapPositionToGrid(
        Vector3 position,
        float gridSize)
    {
        if (gridSize <= 0f)
        {
            return position;
        }

        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.z = Mathf.Round(position.z / gridSize) * gridSize;
        return position;
    }

    private bool ServerHasDependentBuilding(
        NetworkPlacedBuilding support)
    {
        foreach (NetworkObject spawned in
                 NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            NetworkPlacedBuilding building =
                spawned.GetComponent<NetworkPlacedBuilding>();

            if (building != null && building.ServerSupport == support)
            {
                return true;
            }
        }

        return false;
    }

    private void ServerSendResult(ulong targetClientId, string message)
    {
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };

        ShowResultClientRpc(
            new FixedString512Bytes(message),
            rpcParams);
    }

    [ClientRpc]
    private void ShowResultClientRpc(
    FixedString512Bytes message,
    ClientRpcParams rpcParams = default)
    {
        if (!IsOwner)
        {
            return;
        }

        string result = message.ToString();

        if (ownerToast != null)
        {
            ownerToast.Show(result);
        }

        OwnerResultReceived?.Invoke(result);
    }

    public bool ServerTrySpawnRestored(
    NetworkBuildingSaveData saved,
    ulong restoredBuilderClientId,
    out NetworkPlacedBuilding instance)
    {
        instance = null;

        if (!IsServer || saved == null ||
            string.IsNullOrWhiteSpace(saved.buildingId) ||
            string.IsNullOrWhiteSpace(saved.instanceId))
        {
            return false;
        }

        NetworkBuildingEntry entry = FindEntry(saved.buildingId);
        if (entry == null || entry.definition == null ||
            entry.networkPrefab == null)
        {
            Debug.LogWarning($"无法恢复建筑：{saved.buildingId}", this);
            return false;
        }

        instance = Instantiate(
            entry.networkPrefab,
            saved.position,
            Quaternion.Euler(saved.rotation));

        instance.NetworkObject.Spawn(true);
        instance.ServerInitialize(
            restoredBuilderClientId,
            saved.paymentSource,
            null,
            saved.instanceId);

        return true;
    }
}