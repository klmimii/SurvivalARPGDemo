using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 队伍角色切换控制器
/// </summary>
public class PartyController : MonoBehaviour
{
    [SerializeField] private CharacterDefinition[] members;//角色数据数组
    [SerializeField] private Transform visualRoot;//角色的模型挂载点
    [SerializeField] private InputActionReference switchCharacterAction;//切换角色的按键绑定
    [SerializeField] private PlayerController playerController;//引用了玩家的移动控制器和生命值组件，用于动态修改
    [SerializeField] private Health health;
    [SerializeField] private WeaponController weaponController;

    [SerializeField] private PlayerAnimationController animationController;

    private bool acceptLocalInput = true;

    public int ActiveIndex => activeIndex;

    public CharacterDefinition ActiveMember { get; private set; }
    public event Action<CharacterDefinition> ActiveMemberChanged;//当角色切换完成时，他会向外广播，比如UI系统的头像需要跟着变，UI脚本只需要订阅者个事件即可

    private int activeIndex;
    private GameObject activeVisual;

    private void Start()
    {
        //如果队伍里一个角色都没有，就报错并关闭自己
        if (members == null || members.Length == 0)
        {
            Debug.LogError("PartyController 没有配置角色。", this);
            enabled = false;
            return;
        }
        //如果配置征程就默认切换队伍里的第一个角色
        SwitchTo(0);
    }

    private void OnEnable()
    {
        switchCharacterAction.action.Enable();
        switchCharacterAction.action.performed += OnSwitchCharacter;
    }

    private void OnDisable()
    {
        switchCharacterAction.action.performed -= OnSwitchCharacter;
        switchCharacterAction.action.Disable();
    }

    /// <summary>
    /// 切换按键触发后做什么
    /// </summary>
    /// <param name="context"></param>
    private void OnSwitchCharacter(InputAction.CallbackContext context)
    {
        // 远端玩家只显示角色，不读取这台电脑的切人按键。
        if (!acceptLocalInput)
        {
            return;
        }

        //如果是Ui模式 禁止切人
        if (GameBootstrap.InputMode != null && !GameBootstrap.InputMode.IsGameplay())
        {
            return;
        }

        //如果角色已经死了，不允许切人
        if (health.IsDead)
        {
            return;
        }

        int nextIndex = (activeIndex + 1) % members.Length;
        SwitchTo(nextIndex);
    }

    public void SwitchTo(int index)
    {
        //检验传入的索引是否合法
        if (index < 0 || index >= members.Length || members[index] == null)
        {
            return;
        }

        activeIndex = index;
        ActiveMember = members[index];

        //如果当前场景上已经有角色模型了，直接把他销毁掉
        if (activeVisual != null)
        {
            Destroy(activeVisual);
        }

        //实例化角色模型
        if (ActiveMember.visualPrefab != null)
        {
            activeVisual = Instantiate(ActiveMember.visualPrefab, visualRoot);
            activeVisual.transform.localPosition = Vector3.zero;
            activeVisual.transform.localRotation = Quaternion.identity;
            Animator newAnimator = activeVisual != null ? activeVisual.GetComponentInChildren<Animator>() : null;
            animationController.SetAnimator(newAnimator);
        }

        //重新设置最大血量
        health.ConfigureMaxHealth(ActiveMember.maxHealth, true);
        //更新这个角色的移动速度
        playerController.SetMoveSpeed(ActiveMember.moveSpeed);

        if (weaponController != null && ActiveMember.defaultWeapon != null)
        {
            weaponController.Equip(ActiveMember.defaultWeapon);
        }

        //通知全游戏现在换人了，并告知换的是谁
        ActiveMemberChanged?.Invoke(ActiveMember);
    }
    public void SetAcceptLocalInput(bool value)
    {
        acceptLocalInput = value;
    }
}
