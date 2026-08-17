using System.Collections.Generic;
using UnityEngine;

public class LocalRecipeCostService : IRecipeCostService
{
    private readonly IInventoryService inventoryService;

    public LocalRecipeCostService(IInventoryService inventoryService)
    {
        this.inventoryService = inventoryService;
    }

    public bool IsUnlocked(RecipeDefinition recipe)
    {
        if (recipe == null)
        {
            return false;
        }

        if (recipe.unlockItem == null)
        {
            return true;
        }

        return inventoryService.CountItem(recipe.unlockItem) > 0;
    }

    public bool CanConsumeIngredients(
        RecipeDefinition recipe,
        int craftCount = 1)
    {
        if (!IsUnlocked(recipe) || craftCount <= 0)
        {
            return false;
        }

        Dictionary<ItemDefinition, int> amounts =
            BuildIngredientAmounts(recipe, craftCount, 1f);

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

    public InventoryOperationResult TryConsumeIngredients(
        RecipeDefinition recipe,
        int craftCount = 1)
    {
        if (!IsUnlocked(recipe))
        {
            return InventoryOperationResult.Fail("尚未解锁该配方。");
        }

        Dictionary<ItemDefinition, int> amounts =
            BuildIngredientAmounts(recipe, craftCount, 1f);

        if (amounts.Count == 0)
        {
            return InventoryOperationResult.Fail("配方原料配置无效。");
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
                // 原子性回滚：不能只扣掉一部分配方材料。
                foreach (KeyValuePair<ItemDefinition, int> rollback in removed)
                {
                    inventoryService.TryAddItem(
                        rollback.Key,
                        rollback.Value);
                }

                return InventoryOperationResult.Fail(
                    "扣除配方材料失败，已回滚本次操作。");
            }

            removed.Add(pair);
        }

        return InventoryOperationResult.Succeed();
    }

    public InventoryOperationResult TryRefundIngredients(
        RecipeDefinition recipe,
        int craftCount = 1,
        float refundRate = 1f)
    {
        Dictionary<ItemDefinition, int> amounts =
            BuildIngredientAmounts(
                recipe,
                craftCount,
                Mathf.Clamp01(refundRate));

        if (amounts.Count == 0)
        {
            // Refund Rate为0时允许拆除，只是不返还材料。
            return InventoryOperationResult.Succeed("没有可返还的材料。");
        }

        List<KeyValuePair<ItemDefinition, int>> added =
            new List<KeyValuePair<ItemDefinition, int>>();

        foreach (KeyValuePair<ItemDefinition, int> pair in amounts)
        {
            InventoryOperationResult result =
                inventoryService.TryAddItem(pair.Key, pair.Value);

            if (!result.Success)
            {
                // 背包中途满了：撤销本轮已返还部分，建筑继续保留。
                foreach (KeyValuePair<ItemDefinition, int> rollback in added)
                {
                    inventoryService.TryRemoveItem(
                        rollback.Key,
                        rollback.Value);
                }

                return InventoryOperationResult.Fail(
                    "背包空间不足，无法返还配方材料。");
            }

            added.Add(pair);
        }

        return InventoryOperationResult.Succeed("配方材料已返还。");
    }

    private static Dictionary<ItemDefinition, int> BuildIngredientAmounts(
        RecipeDefinition recipe,
        int craftCount,
        float rate)
    {
        Dictionary<ItemDefinition, int> baseAmounts =
            new Dictionary<ItemDefinition, int>();

        if (recipe == null ||
            recipe.ingredients == null ||
            craftCount <= 0)
        {
            return baseAmounts;
        }

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null ||
                ingredient.item == null ||
                ingredient.amount <= 0)
            {
                continue;
            }

            if (!baseAmounts.ContainsKey(ingredient.item))
            {
                baseAmounts[ingredient.item] = 0;
            }

            // 先合并同类原料，再统一计算退款比例，避免重复项分别向下取整。
            baseAmounts[ingredient.item] +=
                ingredient.amount * craftCount;
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