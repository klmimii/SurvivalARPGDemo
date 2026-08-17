using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BossAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Health health;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Slam = Animator.StringToHash("Slam");
    private static readonly int Enrage = Animator.StringToHash("Enrage");
    private static readonly int Die = Animator.StringToHash("Die");

    private void OnEnable()
    {
        health.Died += PlayDeath;
    }

    private void OnDisable()
    {
        health.Died -= PlayDeath;
    }

    private void Update()
    {
        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        animator.SetFloat(Speed, speed, 0.1f, Time.deltaTime);
    }

    public void PlayAttack() => animator.SetTrigger(Attack);
    public void PlaySlam() => animator.SetTrigger(Slam);
    public void PlayEnrage() => animator.SetTrigger(Enrage);
    public void PlayDeath() => animator.SetTrigger(Die);
}
