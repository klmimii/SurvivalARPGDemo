using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInputModeService : MonoBehaviour
{
    //当前的鼠标类型属性，默认为游戏状态
    public GameInputMode CurrentMode { get; private set; } = GameInputMode.Gameplay;
    //模式切换事件,需要传入一个GameInputMode类型的参数
    public event Action<GameInputMode> ModeChanged;

    public void SetMode(GameInputMode mode)
    {
        //如果游戏模式没变，直接返回
        if(CurrentMode==mode)
        {
            return;
        }

        CurrentMode = mode;

        //实时切换鼠标控制状态
        ApplyCursor(mode);
        //触发广播事件，通知所有订阅者
        ModeChanged?.Invoke(mode);
    }

    /// <summary>
    /// 现在是不是游戏状态，提供给外部使用
    /// </summary>
    /// <returns></returns>
    public bool IsGameplay() =>CurrentMode== GameInputMode.Gameplay;
    /// <summary>
    /// 现在是不是建造状态，提供给外部使用
    /// </summary>
    /// <returns></returns>
    public bool IsBuildMode() => CurrentMode == GameInputMode.Build;

    /// <summary>
    /// 鼠标指针的控制中心
    /// </summary>
    /// <param name="mode"></param>
    private void ApplyCursor(GameInputMode mode)
    {
        //逻辑处理，只要是UI模式或Build模式，都需要显示鼠标光标
        bool showCursor = mode == GameInputMode.UI || mode == GameInputMode.Build;

        //控制鼠标显示还是隐藏
        Cursor.visible = showCursor;

        //控制鼠标锁定状态，true不锁定鼠标，false锁定鼠标
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }

    /// <summary>
    /// 这是Unity提供的生命周期内置的回调函数，不需要手动调用，如果玩家切屏，可能让鼠标状态失效，需要重新应用当前规则
    /// </summary>
    /// <param name="focus"></param>
    private void OnApplicationFocus(bool hasFocus)
    {
        //从Alt+Tab返回游戏时，Unity有可能改变鼠标状态，重新应用当前规则
        if(hasFocus)
        {
            ApplyCursor(CurrentMode);
        }
    }
}
