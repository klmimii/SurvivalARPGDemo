using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Health health;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Die = Animator.StringToHash("Die");

    //
    [SerializeField] private EnemyCombat enemyCombat;
    //

    private void OnEnable()
    {
        health.HealthChanged += OnHealthChanged;
        health.Died += PlayDeath;
    }

    private void OnDisable()
    {
        health.HealthChanged -= OnHealthChanged;
        health.Died -= PlayDeath;
    }

    private void Update()
    {
        float normalizedSpeed = agent != null && agent.enabled
            ? agent.velocity.magnitude / Mathf.Max(0.01f, agent.speed)
            : 0f;

        animator.SetFloat(Speed, normalizedSpeed, 0.1f, Time.deltaTime);
    }

    public void PlayAttack()
    {
        animator.SetTrigger(Attack);
    }

    private void OnHealthChanged(int current, int max)
    {
        //if (!health.IsDead)
        //{
        //    animator.SetTrigger(Hit);
        //}

        if (!health.IsDead)
        {
            // 受到伤害触发受击时，强制重置攻击锁
            if (enemyCombat != null)
            {
                enemyCombat.ResetAttackState();
            }

            animator.SetTrigger(Hit);
        }
    }

    private void PlayDeath()
    {
        animator.SetTrigger(Die);
    }
}
