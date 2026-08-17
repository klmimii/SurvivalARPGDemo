using System.Collections.Generic;
using UnityEngine;

public class LocalBuildingService : IBuildingService
{
    private readonly IInventoryService inventoryService;
    private readonly IRecipeCostService recipeCostService;

    public LocalBuildingService(
        IInventoryService inventoryService,
        IRecipeCostService recipeCostService)
    {
        this.inventoryService = inventoryService;
        this.recipeCostService = recipeCostService;
    }

    public bool CanAfford(BuildingDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        // 第一优先级：背包中已经有建筑成品。
        if (definition.requiredKit != null &&
            inventoryService.CountItem(definition.requiredKit) > 0)
        {
            return true;
        }

        // 第二优先级：配方已解锁并且原料足够。
        if (CanUseRecipeFallback(definition))
        {
            return true;
        }

        // 只要配置了快捷建造配方，就不再退回旧Costs路径。
        // 否则配方未解锁时，旧Costs可能绕过解锁规则。
        if (definition.allowIngredientFallback &&
            definition.craftingRecipe != null)
        {
            return false;
        }

        // 第三优先级：仅兼容尚未迁移的旧成本。
        return HasLegacyIngredients(definition);
    }

    public BuildingConsumeResult TryConsumeBuildingCost(
        BuildingDefinition definition)
    {
        if (definition == null)
        {
            return BuildingConsumeResult.Fail("建筑配置为空。");
        }

        // 1. 无论配方是否解锁，都优先消耗已经拥有的建筑成品。
        if (definition.requiredKit != null &&
            inventoryService.CountItem(definition.requiredKit) > 0)
        {
            InventoryOperationResult removeResult =
                inventoryService.TryRemoveItem(
                    definition.requiredKit,
                    1);

            if (removeResult.Success)
            {
                return BuildingConsumeResult.Succeed(
                    BuildingPaymentSource.BuildingItem,
                    $"已使用背包中的{definition.requiredKit.displayName}。");
            }

            return BuildingConsumeResult.Fail(removeResult.Message);
        }

        // 2. 没有成品时，尝试快捷消耗配方原料。
        if (definition.allowIngredientFallback &&
            definition.craftingRecipe != null)
        {
            InventoryOperationResult recipeValidation =
                ValidateBuildingRecipe(definition);

            if (!recipeValidation.Success)
            {
                return BuildingConsumeResult.Fail(
                    recipeValidation.Message);
            }

            InventoryOperationResult consumeResult =
                recipeCostService.TryConsumeIngredients(
                    definition.craftingRecipe);

            if (consumeResult.Success)
            {
                return BuildingConsumeResult.Succeed(
                    BuildingPaymentSource.RecipeIngredients,
                    "建筑成品不足，已直接消耗配方原料。");
            }

            return BuildingConsumeResult.Fail(consumeResult.Message);
        }

        // 3. 旧资产兼容。所有新建筑完成迁移后不会进入这里。
        InventoryOperationResult legacyResult =
            TryConsumeLegacyIngredients(definition);

        if (legacyResult.Success)
        {
            return BuildingConsumeResult.Succeed(
                BuildingPaymentSource.LegacyDirectIngredients,
                "已消耗旧版建筑原料。");
        }

        if (definition.requiredKit != null)
        {
            return BuildingConsumeResult.Fail(
                $"缺少{definition.requiredKit.displayName}，且没有足够的快捷合成原料。");
        }

        return BuildingConsumeResult.Fail(legacyResult.Message);
    }

    public InventoryOperationResult TryRefundBuildingCost(
        BuildingDefinition definition,
        BuildingPaymentSource paymentSource,
        bool forceFullRefund = false)
    {
        if (definition == null)
        {
            return InventoryOperationResult.Fail("建筑配置为空。");
        }

        // Prefab或Socket异常造成的技术回滚必须100%返还；
        // 玩家主动拆除时才使用Definition中的refundRate。
        float actualRefundRate = forceFullRefund
            ? 1f
            : definition.refundRate;

        switch (paymentSource)
        {
            case BuildingPaymentSource.BuildingItem:
                return RefundBuildingItem(
                    definition,
                    actualRefundRate);

            case BuildingPaymentSource.RecipeIngredients:
                return recipeCostService.TryRefundIngredients(
                    definition.craftingRecipe,
                    1,
                    actualRefundRate);

            case BuildingPaymentSource.LegacyDirectIngredients:
                return TryRefundLegacyIngredients(
                    definition,
                    actualRefundRate);

            case BuildingPaymentSource.Unknown:
            default:
                // 旧存档不知道当初如何支付。
                // 保守地返还一个建筑成品，避免重复拆解原料。
                return RefundBuildingItem(
                    definition,
                    actualRefundRate);
        }
    }

    private bool CanUseRecipeFallback(BuildingDefinition definition)
    {
        if (!definition.allowIngredientFallback ||
            definition.craftingRecipe == null)
        {
            return false;
        }

        InventoryOperationResult validation =
            ValidateBuildingRecipe(definition);

        return validation.Success &&
            recipeCostService.CanConsumeIngredients(
                definition.craftingRecipe);
    }

    private static InventoryOperationResult ValidateBuildingRecipe(
        BuildingDefinition definition)
    {
        RecipeDefinition recipe = definition.craftingRecipe;

        if (recipe == null)
        {
            return InventoryOperationResult.Fail("没有配置建筑配方。");
        }

        if (definition.requiredKit == null)
        {
            return InventoryOperationResult.Fail("没有配置建筑成品。");
        }

        if (recipe.outputItem != definition.requiredKit)
        {
            return InventoryOperationResult.Fail(
                "建筑配方产物与Required Kit不一致。");
        }

        if (recipe.outputAmount != 1)
        {
            return InventoryOperationResult.Fail(
                "快捷建造配方必须一次输出一个建筑成品。");
        }

        if (recipe.ingredients == null || recipe.ingredients.Length == 0)
        {
            return InventoryOperationResult.Fail("建筑配方没有原料。");
        }

        return InventoryOperationResult.Succeed();
    }

    private InventoryOperationResult RefundBuildingItem(
        BuildingDefinition definition,
        float refundRate)
    {
        if (definition.requiredKit == null)
        {
            return InventoryOperationResult.Fail("没有可返还的建筑成品。");
        }

        int refundAmount = Mathf.FloorToInt(refundRate);

        if (refundAmount <= 0)
        {
            return InventoryOperationResult.Succeed("该建筑不返还物品。");
        }

        return inventoryService.TryAddItem(
            definition.requiredKit,
            refundAmount);
    }

    private bool HasLegacyIngredients(BuildingDefinition definition)
    {
        Dictionary<ItemDefinition, int> amounts =
            BuildLegacyAmounts(definition, 1f);

        if (amounts.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            if (inventoryService.CountItem(pair.Key) < pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private InventoryOperationResult TryConsumeLegacyIngredients(
        BuildingDefinition definition)
    {
        Dictionary<ItemDefinition, int> amounts =
            BuildLegacyAmounts(definition, 1f);

        if (amounts.Count == 0)
        {
            return InventoryOperationResult.Fail("建筑成本未配置。");
        }

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            if (inventoryService.CountItem(pair.Key) < pair.Value)
            {
                return InventoryOperationResult.Fail(
                    $"缺少{pair.Key.displayName} x{pair.Value}。");
            }
        }

        List<KeyValuePair<ItemDefinition, int>> removed =
            new List<KeyValuePair<ItemDefinition, int>>();

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            InventoryOperationResult result =
                inventoryService.TryRemoveItem(pair.Key, pair.Value);

            if (!result.Success)
            {
                foreach (KeyValuePair<ItemDefinition, int> rollback in removed)
                {
                    inventoryService.TryAddItem(
                        rollback.Key,
                        rollback.Value);
                }

                return result;
            }

            removed.Add(pair);
        }

        return InventoryOperationResult.Succeed();
    }

    private InventoryOperationResult TryRefundLegacyIngredients(
        BuildingDefinition definition,
        float refundRate)
    {
        Dictionary<ItemDefinition, int> amounts =
            BuildLegacyAmounts(definition, refundRate);

        if (amounts.Count == 0)
        {
            return InventoryOperationResult.Succeed("没有可返还的旧版材料。");
        }

        List<KeyValuePair<ItemDefinition, int>> added =
            new List<KeyValuePair<ItemDefinition, int>>();

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            InventoryOperationResult result =
                inventoryService.TryAddItem(pair.Key, pair.Value);

            if (!result.Success)
            {
                foreach (KeyValuePair<ItemDefinition, int> rollback in added)
                {
                    inventoryService.TryRemoveItem(
                        rollback.Key,
                        rollback.Value);
                }

                return InventoryOperationResult.Fail(
                    "背包空间不足，无法返还旧版材料。");
            }

            added.Add(pair);
        }

        return InventoryOperationResult.Succeed("旧版材料已返还。");
    }

    private static Dictionary<ItemDefinition, int> BuildLegacyAmounts(
        BuildingDefinition definition,
        float rate)
    {
        Dictionary<ItemDefinition, int> baseAmounts =
            new Dictionary<ItemDefinition, int>();

        if (definition == null || definition.costs == null)
        {
            return baseAmounts;
        }

        foreach (BuildingCost cost in definition.costs)
        {
            if (cost == null || cost.item == null || cost.amount <= 0)
            {
                continue;
            }

            if (!baseAmounts.ContainsKey(cost.item))
            {
                baseAmounts[cost.item] = 0;
            }

            baseAmounts[cost.item] += cost.amount;
        }

        Dictionary<ItemDefinition, int> finalAmounts =
            new Dictionary<ItemDefinition, int>();

        foreach (KeyValuePair<ItemDefinition, int> pair in baseAmounts)
        {
            int finalAmount = Mathf.FloorToInt(pair.Value * rate);
            if (finalAmount > 0)
            {
                finalAmounts[pair.Key] = finalAmount;
            }
        }

        return finalAmounts;
    }
}

//using System.Collections.Generic;
//using UnityEngine;

//public class LocalBuildingService : IBuildingService
//{
//    private readonly IInventoryService inventoryService;

//    public LocalBuildingService(IInventoryService inventoryService)
//    {
//        this.inventoryService = inventoryService;
//    }

//    public bool CanAfford(BuildingDefinition definition)
//    {
//        Dictionary<ItemDefinition, int> amounts =
//            BuildAmounts(definition, false);

//        if (amounts.Count == 0)
//        {
//            return false;
//        }

//        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
//        {
//            if (inventoryService.CountItem(pair.Key) < pair.Value)
//            {
//                return false;
//            }
//        }

//        return true;
//    }

//    public InventoryOperationResult TryConsumeBuildingCost(
//        BuildingDefinition definition)
//    {
//        Dictionary<ItemDefinition, int> amounts =
//            BuildAmounts(definition, false);

//        if (amounts.Count == 0)
//        {
//            return InventoryOperationResult.Fail("建筑成本未配置。");
//        }

//        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
//        {
//            if (inventoryService.CountItem(pair.Key) < pair.Value)
//            {
//                return InventoryOperationResult.Fail(
//                    $"缺少{pair.Key.displayName} x{pair.Value}。");
//            }
//        }

//        List<KeyValuePair<ItemDefinition, int>> removed =
//            new List<KeyValuePair<ItemDefinition, int>>();

//        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
//        {
//            InventoryOperationResult result =
//                inventoryService.TryRemoveItem(pair.Key, pair.Value);

//            if (!result.Success)
//            {
//                // 原子性回滚：不能只扣除一半材料。
//                foreach (KeyValuePair<ItemDefinition, int> rollback in removed)
//                {
//                    inventoryService.TryAddItem(rollback.Key, rollback.Value);
//                }

//                return result;
//            }

//            removed.Add(pair);
//        }

//        return InventoryOperationResult.Succeed();
//    }

//    public InventoryOperationResult TryRefundBuildingCost(
//        BuildingDefinition definition)
//    {
//        Dictionary<ItemDefinition, int> amounts =
//            BuildAmounts(definition, true);

//        if (amounts.Count == 0)
//        {
//            return InventoryOperationResult.Fail("该建筑没有可返还材料。");
//        }

//        List<KeyValuePair<ItemDefinition, int>> added =
//            new List<KeyValuePair<ItemDefinition, int>>();

//        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
//        {
//            InventoryOperationResult result =
//                inventoryService.TryAddItem(pair.Key, pair.Value);

//            if (!result.Success)
//            {
//                // 如果背包中途满了，把本次已经加入的部分撤回。
//                // 建筑不会被删除，避免玩家损失物品。
//                foreach (KeyValuePair<ItemDefinition, int> rollback in added)
//                {
//                    inventoryService.TryRemoveItem(
//                        rollback.Key,
//                        rollback.Value);
//                }

//                return InventoryOperationResult.Fail(
//                    "背包空间不足，无法拆除并返还材料。");
//            }

//            added.Add(pair);
//        }

//        return InventoryOperationResult.Succeed("材料已返还背包。");
//    }

//    private static Dictionary<ItemDefinition, int> BuildAmounts(
//        BuildingDefinition definition,
//        bool useRefundRate)
//    {
//        Dictionary<ItemDefinition, int> amounts =
//            new Dictionary<ItemDefinition, int>();

//        if (definition == null)
//        {
//            return amounts;
//        }

//        if (definition.costs != null && definition.costs.Length > 0)
//        {
//            foreach (BuildingCost cost in definition.costs)
//            {
//                if (cost == null || cost.item == null || cost.amount <= 0)
//                {
//                    continue;
//                }

//                int amount = useRefundRate
//                    ? Mathf.FloorToInt(cost.amount * definition.refundRate)
//                    : cost.amount;

//                AddAmount(amounts, cost.item, amount);
//            }
//        }
//        else if (definition.requiredKit != null)
//        {
//            int amount = useRefundRate
//                ? Mathf.FloorToInt(definition.refundRate)
//                : 1;

//            AddAmount(amounts, definition.requiredKit, amount);
//        }

//        return amounts;
//    }

//    private static void AddAmount(
//        Dictionary<ItemDefinition, int> amounts,
//        ItemDefinition item,
//        int amount)
//    {
//        if (item == null || amount <= 0)
//        {
//            return;
//        }

//        if (!amounts.ContainsKey(item))
//        {
//            amounts[item] = 0;
//        }

//        amounts[item] += amount;
//    }
//}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class LocalBuildingService : IBuildingService
//{
//    //声明了一个背包服务接口对象
//    private readonly IInventoryService inventoryService;

//    //通过构造函数把现有的IInventoryService传进来
//    public LocalBuildingService(IInventoryService inventoryService)
//    {
//        this.inventoryService = inventoryService;
//    }
//    public InventoryOperationResult TryConsumeBuildingKit(BuildingDefinition definition)
//    {
//        //如果传进来的建筑配置对象是空，或者没有配置需要消耗的工具包，代码会提前拦截
//        if (definition == null || definition.requiredKit == null)
//        {
//            return InventoryOperationResult.Fail("建筑配置不完整。");
//        }
//        //实际业务调用，去背包里扣除一个指定的建筑工具包
//        return inventoryService.TryRemoveItem(definition.requiredKit, 1);
//    }
//}
