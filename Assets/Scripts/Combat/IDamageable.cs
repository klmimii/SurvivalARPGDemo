using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伤害接口，写成接口是因为玩家攻击时不应该先判断他是不是EnemyController。攻击只关心目标能否受伤，未来Boss,木箱等可破坏建筑也可以实现IDamageable
/// </summary>
public interface IDamageable //只要某个物体能被扣血，就必须实现一个叫做TakeDamage的方法
{
    void TakeDamage(int damage);
}
