//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// 本地合成逻辑：合法性校验，扣除原材料，发放合成产物
///// </summary>
//public class LocalCraftingService : ICraftingService
//{
//    //通过构造函数注入了背包服务IIventroyService
//    private readonly IInventoryService inventoryService;
//    //LocalCraftingService本身并不直接管理背包的数据结构，而是通过接口来查询、扣除和添加物品，职责划分的非常清晰

//    public LocalCraftingService(IInventoryService inventoryService)
//    {
//        this.inventoryService = inventoryService;
//    }

//    /// <summary>
//    /// 判断合成的前置条件是否满足
//    /// </summary>
//    /// <param name="recipe"></param>
//    /// <returns>要合成的配方</returns>
//    public bool CanCraft(RecipeDefinition recipe)
//    {
//        if(recipe==null||recipe.outputItem==null||recipe.ingredients==null)
//        {
//            return false;
//        }

//        //遍历配方资产中的每一种原材料，用inventoryService.CountItem查询背包里的持有量是否大于或等于配方所需数量
//        foreach(RecipeIngredient ingredient in recipe.ingredients)
//        {
//            //如果原材料的原料为空或者原材料的数量小于等于0，说明配方有误，直接返回false
//            if(ingredient.item==null||ingredient.amount<=0)
//            {
//                return false;
//            }

//            //如果配方无误，调用背包服务查询当前种原料的数量是否小于需要的原料的数量，如果小于，直接返回false
//            if(inventoryService.CountItem(ingredient.item)<ingredient.amount)
//            {
//                return false;
//            }
//        }
//        //如果配方本身、产物以及材料数组都不为空，原料配方也没问题，背包中材料也足够的话，调用背包数据中的CanAdd方法判断能不能放进背包，可以就返回true，不行就返回false
//        //避免先扣材料后发现成品没位置放
//        return GameBootstrap.InventoryModel.CanAdd(recipe.outputItem, recipe.outputAmount);
//    }


//    /// <summary>
//    /// 执行真正的合成扣除与发放流程
//    /// </summary>
//    /// <param name="recipe"></param>
//    /// <returns></returns>
//    public CraftingResult TryCraft(RecipeDefinition recipe)
//    {
//        //真正改动数据前调用CanCraft，任何一项不满足立刻拦截并返回失败提示
//        if(!CanCraft(recipe))
//        {
//            return CraftingResult.Fail("材料不足或背包空间不足");
//        }

//        //canCraft以保证足够；此处仍逐个检查返回值，代码更安全
//        foreach(RecipeIngredient ingredient in recipe.ingredients)
//        {
//            InventoryOperationResult removeResult = inventoryService.TryRemoveItem(ingredient.item, ingredient.amount);

//            if(!removeResult.Success)
//            {
//                return CraftingResult.Fail("制作过程中扣除材料失败。");
//            }
//        }

//        //发放产物与结果返回
//        InventoryOperationResult addResult = inventoryService.TryAddItem(recipe.outputItem, recipe.outputAmount);
//        //原型中不会到达这里，因为前面已CanAdd，生产环境应设计事务/回滚，保证扣除与产出原子性
//        //什么是回滚/原子性危机。如在TryRemove(A)成功，但removeb失败或者Add产物失败，此时因为A已经扣除了且无法回复产物也没那到
//        //策略A：先消耗，后回滚，记录扣除成功的材料列表，后续任何一步失败，把扣除的材料重新加回背包。策略B打包原子事务，将扣除材料和添加产物作为一个原子操作提交给背包处理
//        if(!addResult.Success)
//        {
//            return CraftingResult.Fail("制作完成，但背包添加失败。");
//        }

//        //调用全局入口中的事件总线的配方合成方法
//        GameBootstrap.Events.PublishRecipeCrafted(recipe);

//        //扣除成功后 向背包成功放入合成的产物，并返回包含成功状态与文本信息的CraftingResult对象
//        return CraftingResult.Succeed($"制作完成，{recipe.outputItem.displayName}x{recipe.outputAmount}");
//    }
//}

public class LocalCraftingService : ICraftingService
{
    private readonly IInventoryService inventoryService;
    private readonly IRecipeCostService recipeCostService;

    public LocalCraftingService(
        IInventoryService inventoryService,
        IRecipeCostService recipeCostService)
    {
        this.inventoryService = inventoryService;
        this.recipeCostService = recipeCostService;
    }

    public bool IsUnlocked(RecipeDefinition recipe)
    {
        return recipeCostService.IsUnlocked(recipe);
    }

    public bool CanCraft(RecipeDefinition recipe)
    {
        if (recipe == null ||
            recipe.outputItem == null ||
            recipe.outputAmount <= 0)
        {
            return false;
        }

        if (!recipeCostService.CanConsumeIngredients(recipe))
        {
            return false;
        }

        return GameBootstrap.InventoryModel != null &&
            GameBootstrap.InventoryModel.CanAdd(
                recipe.outputItem,
                recipe.outputAmount);
    }

    public CraftingResult TryCraft(RecipeDefinition recipe)
    {
        if (!IsUnlocked(recipe))
        {
            return CraftingResult.Fail("尚未获得该配方。");
        }

        if (recipe == null ||
            recipe.outputItem == null ||
            recipe.outputAmount <= 0)
        {
            return CraftingResult.Fail("配方产物配置无效。");
        }

        if (!CanCraft(recipe))
        {
            return CraftingResult.Fail("材料不足或背包空间不足。");
        }

        InventoryOperationResult consumeResult =
            recipeCostService.TryConsumeIngredients(recipe);

        if (!consumeResult.Success)
        {
            return CraftingResult.Fail(consumeResult.Message);
        }

        InventoryOperationResult addResult =
            inventoryService.TryAddItem(
                recipe.outputItem,
                recipe.outputAmount);

        if (!addResult.Success)
        {
            recipeCostService.TryRefundIngredients(recipe);
            return CraftingResult.Fail(
                "添加制作产物失败，配方材料已回滚。");
        }

        // 保留你第13天增加的任务事件。
        if (GameBootstrap.Events != null)
        {
            GameBootstrap.Events.PublishRecipeCrafted(recipe);
        }

        return CraftingResult.Succeed(
            $"制作完成：{recipe.outputItem.displayName} " +
            $"x{recipe.outputAmount}");
    }
}