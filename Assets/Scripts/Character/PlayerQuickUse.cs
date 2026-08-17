using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerQuickUse : MonoBehaviour
{
    [SerializeField]
    private InputActionReference usePotionAction;//输入动作引用
    [SerializeField]
    private ItemDefinition healingPotion;//指定快捷键使用的绑定的具体物品数据（比如血瓶）
    [SerializeField]
    private ToastView toastView;//屏幕上的提示框。选服UI，用于显示使用成功或生命值已满等反馈信息

    //缓存当前挂载该脚本的玩家自身的Health组件
    private Health health;

    private void Awake()
    {
        //在初始化阶段获取自身的Health组件
        health = this.GetComponent<Health>();
    }

    private void OnEnable()
    {   
        //当脚本或物体被激活时，开启输入监听
        usePotionAction.action.Enable();
        //绑定事件处理，当玩家按键满足触发条件时，调用OnUsePotion
        usePotionAction.action.performed += OnUsePotion; 
    }

    private void OnDisable()
    {
        //当脚本禁用或角色销毁时，解绑事件
        usePotionAction.action.performed -= OnUsePotion;
        //禁用输入监听
        usePotionAction.action.Disable();
    }

    /// <summary>
    /// 按键响应
    /// </summary>
    /// <param name="context"></param>
    private void OnUsePotion(InputAction.CallbackContext context)
    {
        //尝试使用物品
        InventoryOperationResult result = ConsumableItemService.TryUse(healingPotion, health);
        //根据结果显示浮动文字
        toastView.Show(result.Message);
    }
    
}
