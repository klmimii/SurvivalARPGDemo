using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生命值管理组件
/// 核心设计思想是解耦和数据封装。目的是让血量计算这件事只在他内部搞定，其它系统只需要监听他的通知而不需要把逻辑混在一起
/// </summary>
public class Health : MonoBehaviour,IDamageable
{
    [SerializeField]//特性：让私有变量也能显示在Inspector窗口中 方便观察和修改
    private int maxHealth = 100;
    //最大血量
    //MaxHealth属性，只读，外部脚本可以读取最大血量，但是不能修改它
    public int MaxHealth => maxHealth;
    //当前血量
    public int CurrentHealth
    {
        //public 的Get和private的Set，任何脚本都可以随时读取当前血量，但只有Health组件内部可以修改当前血量
        //如果不写private set，那么这个属性就只读了，没法写
        get;
        private set;
    }
    //死亡状态标记
    public bool IsDead
    {
        get;
        private set;
    }


    //UI可以订阅这个时间来刷新血条，只要血条变化，Health就会广播这个事件
    //event Action是一个带有两个参数（当前血量，最大血量）的事件，不带event就是一个普通委托，带了event就是事件，事件外部只能订阅-=+=，外部不能主动调用这个委托，只能定义了这个事件的脚本内部能调用
    public event Action<int, int> HealthChanged;
    //死亡事件
    public event Action Died;



    private void Awake()
    {
        //在游戏刚开始，组件被加载的第一时间，吧当前血量填满为最大血量
        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        // 在 Start 中广播一次初始血量，确保此时所有 UI 订阅者都能收到通知并刷新！
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    //扣血逻辑
    public void TakeDamage(int damage)
    {
        //1.安全边界检查，如果已经死了，或者传入的伤害数值不合法（<=0）直接拦截
        if(damage<=0||IsDead)
        {
            return;
        }

        //2.扣血并限制下限，Mathf.Max确保血量最低降到00，不会出现负数血量
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage) ;

        //3.触发血量变更事件
        HealthChanged?.Invoke(CurrentHealth,maxHealth);

        //4.判定死亡
        if(CurrentHealth==0)
        {
            IsDead = true;
            //触发死亡事件
            Died?.Invoke();
        }

    }

    public void Heal(int amount)
    {
        //1.安全便捷检查：死人不能吃药，加血数值必须大于0
        if(IsDead||amount<=0)
        {
            return;
        }

        //2.加血并限制上限，mathf.min确保血量最高不会超过MaxHealth
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);

        //3.触发血量变更事件，通知UI更新血条
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>
    /// 恢复生命值的方法
    /// </summary>
    /// <param name="value"></param>
    public void RestoreHealth(int value)
    {
        if (IsDead)
        {
            return;
        }

        CurrentHealth = Mathf.Clamp(value, 1, MaxHealth);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    //配置最大血量，支持角色切换时配置
    public void ConfigureMaxHealth(int newMaxHealth, bool fillToMax)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);

        //如果要满血则设置为最大血量
        if (fillToMax)
        {
            CurrentHealth = maxHealth;
        }
        else
        {
            //当前血量大于零小于maxhealth
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, maxHealth);
        }

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>
    /// 只供 NetworkPlayerState 把服务器状态应用到本地表现层。
    /// 它不会自行计算伤害。
    /// </summary>
    public void ApplyNetworkState(int current, int maximum)
    {
        bool wasDead = IsDead;

        maxHealth = Mathf.Max(1, maximum);
        CurrentHealth = Mathf.Clamp(current, 0, maxHealth);
        IsDead = CurrentHealth <= 0;

        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (IsDead && !wasDead)
        {
            Died?.Invoke();
        }
    }
}
