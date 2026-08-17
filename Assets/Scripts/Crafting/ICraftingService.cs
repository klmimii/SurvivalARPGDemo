using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 合成服务接口
/// </summary>
public interface ICraftingService 
{
    //尝试根据给定配方进行一次合成
    //CraftingResult TryCraft(RecipeDefinition recipe);

    ////检查当前是否具备合成该配方的条件，这个仅用于UI状态刷新，比如再合成界面打开时，遍历显示配方列表，并根据CanCraft的返回值将合成按钮设置成高亮或置暗

    //bool CanCraft(RecipeDefinition recipe);

    bool IsUnlocked(RecipeDefinition recipe);
    bool CanCraft(RecipeDefinition recipe);
    CraftingResult TryCraft(RecipeDefinition recipe);

}
