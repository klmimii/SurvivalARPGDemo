using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Health health;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int Dodge = Animator.StringToHash("Dodge");
    private static readonly int Die = Animator.StringToHash("Die");
    private static readonly int AttackDagger = Animator.StringToHash("AttackDagger");
    private static readonly int AttackBow = Animator.StringToHash("AttackBow");
    private static readonly int AttackLongBlade = Animator.StringToHash("AttackLongBlade");

    private static readonly int IsAiming = Animator.StringToHash("IsAiming");

    private void OnEnable()
    {
        playerController.JumpStarted += PlayJump;
        playerController.DodgeStarted += PlayDodge;
        health.Died += PlayDeath;
    }

    private void OnDisable()
    {
        playerController.JumpStarted -= PlayJump;
        playerController.DodgeStarted -= PlayDodge;
        health.Died -= PlayDeath;
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(Speed, playerController.NormalizedMoveSpeed, 0.1f, Time.deltaTime);
        animator.SetBool(IsGrounded, playerController.IsGrounded);
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
    }

    public void PlayJump()
    {
        if (animator != null) animator.SetTrigger(Jump);
    }

    public void PlayDodge()
    {
        if (animator != null) animator.SetTrigger(Dodge);
    }

    public void PlayDeath()
    {
        if (animator != null) animator.SetTrigger(Die);
    }

    public void SetAiming(bool aiming)
    {
        if (animator != null)
        {
            animator.SetBool(IsAiming, aiming);
        }
    }

    public void PlayAttack(WeaponType weaponType)
    {
        if (animator == null)
        {
            return;
        }

        switch (weaponType)
        {
            case WeaponType.Dagger:
                animator.SetTrigger(AttackDagger);
                break;
            case WeaponType.Bow:
                animator.SetTrigger(AttackBow);
                break;
            case WeaponType.LongBlade:
                animator.SetTrigger(AttackLongBlade);
                break;
        }
    }
}