using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器定义
/// </summary>
[CreateAssetMenu(menuName = "Survival ARPG/Weapon Definition", fileName = "Weapon_")]
public class WeaponDefinition : ScriptableObject
{
    public string weaponId;//武器Id
    public string displayName;//展示名字
    public WeaponType weaponType;//武器类型
    [Min(1)] public int damage = 10;///武器伤害
    [Min(0.05f)] public float cooldown = 0.5f;//武器冷却时间

    [Header("Melee")]
    [Min(0.1f)] public float attackRadius = 1f;//攻击半径

    [Header("Bow")]
    public Projectile projectilePrefab;//箭矢预设体
    [Min(1f)] public float projectileSpeed = 15f;//飞行速度
}
