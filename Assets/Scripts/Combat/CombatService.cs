using System;
using UnityEngine;

public static class CombatService
{
    public static event Action<CombatDamageEvent> DamageApplied;

    public static bool TryDealDamage(
        Component targetComponent,
        int damage)
    {
        if (targetComponent == null || damage <= 0)
        {
            return false;
        }

        IDamageable damageable =
            targetComponent.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            return false;
        }

        Health targetHealth =
            targetComponent.GetComponentInParent<Health>();

        int healthBefore = targetHealth != null
            ? targetHealth.CurrentHealth
            : -1;

        damageable.TakeDamage(damage);

        int appliedDamage = damage;

        if (targetHealth != null)
        {
            appliedDamage = Mathf.Max(
                0,
                healthBefore - targetHealth.CurrentHealth);
        }

        // 例如目标已经死亡、处于无敌状态或伤害被完全抵消。
        if (appliedDamage <= 0)
        {
            return false;
        }

        CombatantIdentity identity =
            targetComponent.GetComponentInParent<CombatantIdentity>();

        Vector3 feedbackPosition = identity != null
            ? identity.FeedbackPosition
            : targetComponent.transform.position;

        DamageApplied?.Invoke(new CombatDamageEvent(
            targetComponent,
            identity,
            feedbackPosition,
            damage,
            appliedDamage));

        return true;
    }

    /// <summary>
    /// 网络层已经完成伤害计算后，只发布本地表现事件。
    /// 这里不会再次扣血。
    /// </summary>
    public static void PublishNetworkDamage(
        Component targetComponent,
        int appliedDamage)
    {
        if (targetComponent == null || appliedDamage <= 0)
        {
            return;
        }

        CombatantIdentity identity =
            targetComponent.GetComponentInParent<CombatantIdentity>();

        Vector3 feedbackPosition = identity != null
            ? identity.FeedbackPosition
            : targetComponent.transform.position;

        DamageApplied?.Invoke(new CombatDamageEvent(
            targetComponent,
            identity,
            feedbackPosition,
            appliedDamage,
            appliedDamage));
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// 静态战斗服务类，使用静态类是为了减少原型的初始化负担，真正项目里他会发展为可替换的ICombatService，从而预留服务端实现
///// </summary>
////设计成静态工具无需实例化，也不需要挂载到GameObject上，任何攻击系统（比如玩家的挥剑脚本，子弹脚本，怪物技能脚本）都可以随时随地直接通过tryDealDamage,没有无谓的内存开销
//public static class CombatService 
//{
//    //当前值是最简单的结算入口
//    //后续可以在这里加入暴击，防御，元素反应，Buff或联网校验

//    /// <summary>
//    ///尝试造成伤害
//    /// </summary>
//    /// <param name="targetComponent"></param>
//    /// <param name="damage"></param>
//    /// <returns></returns>
//    public static bool TryDealDamage(Component targetComponent, int damage)
//    {
//        ////这里用的不是GetComponent，而是GetComponentInPatent。是因为复杂角色的碰撞器往往不是挂在最顶层的故物体上，而是挂载在子物体或者骨骼节点上
//        ////比如怪物的碰撞体可能挂在头部，而管理总血量的脚本（继承了IDamageable）通常挂在最外层的父物体上
//        //IDamageable damageable = targetCollider.GetComponentInParent<IDamageable>();

//        ////判断受到攻击的对象是否是可受伤的
//        //if (damageable ==null)
//        //{
//        //    //如果返回false，说明砍到墙壁或者打空了，可以播放砍在石头上的火花或者普通的挥空音效
//        //    return false;
//        //}

//        //damageable.TakeDamage(damage);
//        ////通过返回bool调用方如武器可以知道这次攻击有没有造成伤害，如果返回true,那么武器可以播放受击音效或者卡刀效果
//        //return true;


//        if (targetComponent == null || damage <= 0)
//        {
//            return false;
//        }

//        IDamageable damageable = targetComponent.GetComponentInParent<IDamageable>();
//        if (damageable == null)
//        {
//            return false;
//        }

//        damageable.TakeDamage(damage);
//        return true;
//    }
//}
