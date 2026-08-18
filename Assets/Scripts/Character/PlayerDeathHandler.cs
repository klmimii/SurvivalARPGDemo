using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerDeathHandler : MonoBehaviour
{
    private Health health;

    private void Awake()
    {
        health = this.GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.Died += OnDied;
    }

    private void Onisable()
    {
        health.Died -= OnDied;
    }

    private void OnDied()
    {
        Debug.Log("玩家死亡：当前原型先停止角色移动，后续可显示复活界面");

        PlayerController controller = this.GetComponent<PlayerController>();
        PlayerCombat combat = this.GetComponent<PlayerCombat>();
        PlayerInteraction interaction = this.GetComponent<PlayerInteraction>();

        if (controller != null)
            controller.enabled = false;//禁用移动和跳跃等控制输入
        if (combat != null)
            combat.enabled = false;//禁用攻击与技能释放
        if (interaction != null)
            interaction.enabled = false;//禁用与场景物体的交互功能 

    }
}
