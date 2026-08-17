using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CraftingView : MonoBehaviour
{
    [SerializeField]
    private Transform contentRoot;//Unity中创建的Content挂载点，新生成的配方槽位都会作为他的子物体排版
    [SerializeField]
    private RecipeSlotView recipeSlotPrefab;//预先制作好的RecipeSlotView预制体，每次渲染时会拿他来实例化
    [SerializeField]
    private Button closeButton;//关闭界面的按钮
    //用来记录当前界面里已经实例化的槽位视图列表，方便后续清理
    private readonly List<RecipeSlotView> activeSlots = new List<RecipeSlotView>();
    //面板关闭时的C#事件
    public event Action Closed;

    //初始化时自动为关闭按钮绑定Close()方法
    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
    }

    public void Show()
    {
        this.gameObject.SetActive(true);

    }

    public void Close()
    {
        this.gameObject.SetActive(false);
        //对象失活仍然可以执行，失活后只有协程或者Unity生命周期事件（如Update,FixedUpdate）不执行，其他都会执行
        Closed?.Invoke();
    }

    /// <summary>
    /// 上层控制器用来刷新合成界面的核心入口，
    /// </summary>
    /// <param name="recipes">需要展示的所有配方数组</param>
    /// <param name="canCraft">是Unity内置的泛型委托，用来拿到材料够不够，第一个参数是接收参数类型，第二个是返回值类型</param>
    /// <param name="onCraft">点击制作时的回调函数</param>
    public void Render(RecipeDefinition[] recipes,Func<RecipeDefinition,bool> canCraft,Action<RecipeDefinition> onCraft)
    {
        //先调用ClearSlot清空旧槽位
        ClearSlots();

        //循环遍历配方
        foreach(RecipeDefinition recipe in recipes)
        {
            //实例化新槽位并挂在ContentRoot下
            RecipeSlotView slot = Instantiate(recipeSlotPrefab, contentRoot);
            //调用方法填充数据并绑定点击事件
            slot.Bind(recipe, canCraft(recipe), onCraft);
            //记录到activeSlots列表
            activeSlots.Add(slot);
        }
    }

    /// <summary>
    /// 再重新绘制配方列表之前，把上一次生成的槽位GameObject彻底销毁，并清空activeSlot列表，防止列表不断叠加膨胀造成界面重叠
    /// </summary>
    private void ClearSlots()
    {
        foreach(RecipeSlotView slot in activeSlots)
        {
            Destroy(slot.gameObject);
        }

        activeSlots.Clear();
    }
}
