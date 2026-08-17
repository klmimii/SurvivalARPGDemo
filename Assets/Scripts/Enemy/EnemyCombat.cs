using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private EnemyAnimationController animationController;

    private float nextAttackTime;
    private Transform pendingTarget;

    public bool TryAttack(Transform target)
    {
        if (target == null || pendingTarget != null || Time.time < nextAttackTime)
        {
            return false;
        }

        Health targetHealth = target.GetComponentInParent<Health>();
        if (targetHealth == null || targetHealth.IsDead)
        {
            return false;
        }

        nextAttackTime = Time.time + attackCooldown;
        pendingTarget = target;
        animationController.PlayAttack();
        return true;
    }

    // Enemy Attack Clip 的命中帧调用。
    public void AnimationEvent_AttackHit()
    {
        if (pendingTarget == null)
        {
            return;
        }

        Health targetHealth = pendingTarget.GetComponentInParent<Health>();
        if (targetHealth != null && !targetHealth.IsDead)
        {
            CombatService.TryDealDamage(targetHealth, damage);
        }
    }

    // Enemy Attack Clip 的末尾调用。
    public void AnimationEvent_AttackFinished()
    {
        pendingTarget = null;
    }

    /// <summary>
    /// 当被打断或受击时重置攻击状态
    /// </summary>
    public void ResetAttackState()
    {
        pendingTarget = null;
    }
}
