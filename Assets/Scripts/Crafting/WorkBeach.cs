using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 原型中Workbeach使用Present足够清楚，后续多场景/多工作台时，可让工作台发送OpenCraftingStationEvent，由UI流程层统一打开界面
/// </summary>
public class WorkBeach : MonoBehaviour,IInteractable
{
    [SerializeField]
    //在场景中吧挂载UIManager的对象拖拽赋值给他，这样工作台就有控制合成界面打开的权限
    private UIManager uiManager;

    //定义静态事件，当任何工作台被使用时广播
    public static event Action OnInteract;

    public string GetPromptText()
    {
        //当玩家靠近工作台时UI系统可以调用这个方法。获取并显示提示文字
        return "按E使用工作台";
    }

    public bool TryInteract(GameObject interactor)
    {
        //打开UI界面并刷新可合成的配方列表
        //craftingPresenter.Open();

        //交互后打开背包界面
        uiManager.OpenCrafting();
        OnInteract?.Invoke();
        //向调用方反馈交互成功执行，通知玩家控制器可以播放动作，播放音效或暂停移动等
        return true;
    }
}
