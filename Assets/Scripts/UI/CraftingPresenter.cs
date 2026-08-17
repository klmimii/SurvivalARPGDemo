using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftingPresenter : MonoBehaviour
{
    [SerializeField]
    private CraftingView craftingView;//合成界面的UI视图
    [SerializeField]
    private RecipeDefinition[] recipe;//配方文件数组
    [SerializeField]
    private ToastView toastView;//提示框/通知组件

    private void Start()
    {
        //订阅事件，当玩家背包/库存发生变化时触发RefreshIfVisable方法
        GameBootstrap.InventoryModel.Changed += RefreshIfVisible;
    }

    private void OnDestroy()
    {
        //取消订阅，在脚本/对象被销毁时取消事件监听，
        //之所以要判空是因为一个对象在销毁或退出场景时，各种脚本被销毁的顺序是不固定的，可能在执行到这的时候Inventory已经被销毁了，此时在取消订阅就会报错
        if(GameBootstrap.InventoryModel!=null)
        {
            GameBootstrap.InventoryModel.Changed -= RefreshIfVisible;
        }
    }

    /// <summary>
    /// 显示合成界面并立即刷新数据
    /// </summary>
    public void Open()
    {
        craftingView.Show();
        Refresh();
    }

    /// <summary>
    /// 吧要在界面展示的配方数据、一个检查方法让View知道某个配方当前是否满足合成条件，点击合成按钮时的回调函数传给VIew 
    /// </summary>
    public void Refresh()
    {
        craftingView.Render(recipe, GameBootstrap.CraftingService.CanCraft, TryCraft);
    }

    /// <summary>
    /// 如果当前界面可见就刷新，只有当合成界面处于激活/可见状态时才刷新UI，避免在界面隐藏时做无用的渲染消耗
    /// </summary>
    private  void RefreshIfVisible()
    {
        if(craftingView.gameObject.activeInHierarchy)
        {
            Refresh();
        }
    }

    private void TryCraft(RecipeDefinition recipe)
    {
        CraftingResult result = GameBootstrap.CraftingService.TryCraft(recipe);
        toastView.Show(result.Message);
        Refresh();
    }


}
