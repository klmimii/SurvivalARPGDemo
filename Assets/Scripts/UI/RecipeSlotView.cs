using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个配方槽位的Ui信息并响应玩家点击制作按钮的操作
/// </summary>
public class RecipeSlotView : MonoBehaviour
{
    [SerializeField]
    private TMP_Text nameText;//配方名称
    [SerializeField]
    private TMP_Text ingredientsText;//消耗材料列表
    [SerializeField]
    private TMP_Text stateText;//当前是否可制作的状态提示
    [SerializeField]
    private Button craftButton;//合成按钮

    //缓存当前槽位所绑定的RecipeDefinition数据资产
    private RecipeDefinition recipe;
    //玩家点击按钮时调用委托函数
    private Action<RecipeDefinition> onCraftClicked;

    /// <summary>
    /// 数据绑定与刷新
    /// </summary>
    /// <param name="recipeDefinition">配方</param>
    /// <param name="canCraft">能否合成</param>
    /// <param name="clickCallback">点击按钮的事件</param>
    public void Bind(RecipeDefinition recipeDefinition,bool canCraft,Action<RecipeDefinition> clickCallback)
    {
        //文本赋值，更新名称，拼装材料字符串，并根据canCraft传入的bool值显示可制作或材料不足背包已满
        recipe = recipeDefinition;
        onCraftClicked = clickCallback;

        nameText.text = recipe.displayName;
        ingredientsText.text = BuildIngredientText(recipe);
        //如果材料不足或背包满了，canCraft为false，按钮会自动变灰置暗的不可点击状态
        stateText.text = canCraft ? "可制作" : "材料不足/背包已满";
        craftButton.interactable = canCraft;

        //非常关键，在添加新的监听前，先清空之前绑定的监听器。因为这个UI槽位可能会在列表重用/对象池中被多次Bind，不清理会导致点击一次出发多次历史回溯
        craftButton.onClick.RemoveAllListeners();
        //绑定当前槽位的点击响应
        craftButton.onClick.AddListener(OnCraftClicked);
    }

    //拼接材料文本
    private string BuildIngredientText(RecipeDefinition recipeDefinition)
    {
        string result = "材料：";
        //遍历配方里的材料数组，将所有需要的物品名称和数量拼接成一句话
        foreach(RecipeIngredient ingredient in recipeDefinition.ingredients)
        {
            result += $"{ingredient.item.displayName}x{ingredient.amount}";
        }
        //例如最终生成字符串材料：木材x2 怪物素材x1 
        return result;
    }

    private void OnCraftClicked()
    {
        onCraftClicked?.Invoke(recipe);
    }
}
