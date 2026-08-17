using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    private WeaponController weaponController;

    private void Awake()
    {
        // 游戏运行时自动向上在父节点（即 Player 根物体）查找 WeaponController
        weaponController = GetComponentInParent<WeaponController>();

        if (weaponController == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 的父物体上未找到 WeaponController 组件！");
        }
    }

    public void AnimationEvent_AttackHit()
    {
        if (weaponController != null)
        {
            weaponController.AnimationEvent_AttackHit();
        }
    }

    public void AnimationEvent_AttackFinished()
    {
        if (weaponController != null)
        {
            weaponController.AnimationEvent_AttackFinished();
        }
    }
}