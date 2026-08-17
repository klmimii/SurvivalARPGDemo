

/// <summary>
/// 配方脚本，用来完整描述消耗什么材料，能合成什么产物，合成多少个
/// </summary>
//[CreateAssetMenu(menuName ="Survival ARPG/Recipe Definition",fileName ="Recipe_")]
//public class RecipeDefinition :ScriptableObject
//{
//    public string recipeId;//配方的唯一标识符 
//    public string displayName;//再UI的合成台中展示的名称
//    [TextArea]
//    public string description;//配方描述

//    public RecipeIngredient[] ingredients;//因为RecipeIngradient添加了Serializeable特性，可以在UnityInspector面暗中动态添加多个输入材料
//    public ItemDefinition outputItem;//指向合成成功后玩家获得的产物
//    [Min(1)]
//    public int outputAmount = 1;//一次合成和以获得该物品的数量，默认是1

//}

using UnityEngine;

[CreateAssetMenu(
    menuName = "Survival ARPG/Recipe Definition",
    fileName = "Recipe_")]
public class RecipeDefinition : ScriptableObject
{
    [Tooltip("稳定唯一ID，存档、任务和未来网络可能使用。")]
    public string recipeId;

    public string displayName;

    [TextArea]
    public string description;

    [Header("Unlock")]
    [Tooltip("玩家必须拥有的配方图纸物品；为空表示默认解锁。")]
    public ItemDefinition unlockItem;

    [Header("Crafting")]
    public RecipeIngredient[] ingredients;
    public ItemDefinition outputItem;

    [Min(1)]
    public int outputAmount = 1;
}