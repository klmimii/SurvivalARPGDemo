using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 怪物真实血量只由服务器写。
/// 本地 Health 只作为血条等旧表现层的数据适配器。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkEnemyHealth : NetworkBehaviour
{
    [Header("Health")]
    [Min(1)]
    [SerializeField]
    private int configuredMaxHealth = 100;

    [Min(0f)]
    [SerializeField]
    private float despawnDelay = 2f;

    [Header("Existing Presentation")]
    [SerializeField]
    private Health presentationHealth;

    [SerializeField]
    private NetworkAnimator networkAnimator;

    [SerializeField]
    private NetworkEnemyAI enemyAI;

    [SerializeField]
    private Collider[] combatColliders;

    [Header("Drop")]
    [SerializeField]
    private NetworkPickupItem pickupPrefab;

    [SerializeField]
    private ItemDefinition dropItem;

    [Min(1)]
    [SerializeField]
    private int dropAmount = 1;

    private readonly NetworkVariable<int> currentHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> maxHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> dead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int CurrentHealth => currentHealth.Value;
    public int MaxHealth => maxHealth.Value;
    public bool IsDead => dead.Value;

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;
        maxHealth.OnValueChanged += OnHealthChanged;
        dead.OnValueChanged += OnDeadChanged;

        if (IsServer)
        {
            maxHealth.Value = Mathf.Max(1, configuredMaxHealth);
            currentHealth.Value = maxHealth.Value;
            dead.Value = false;
        }

        ApplyHealthPresentation();
        ApplyDeadPresentation(dead.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
        maxHealth.OnValueChanged -= OnHealthChanged;
        dead.OnValueChanged -= OnDeadChanged;
    }

    /// <summary>
    /// 只允许服务器战斗代码调用。
    /// attacker 用于给 AI 增加伤害仇恨。
    /// </summary>
    public bool ServerTakeDamage(
     int damage,
     NetworkObject attacker)
    {
        if (!IsServer || damage <= 0 || dead.Value)
        {
            return false;
        }

        currentHealth.Value = Mathf.Max(
            0,
            currentHealth.Value - damage);

        if (enemyAI != null)
        {
            // Attack可能被Hit动画中断，先解除上一次攻击锁。
            enemyAI.ServerInterruptAttack();

            if (attacker != null)
            {
                enemyAI.ServerAddThreat(attacker.transform, damage);
            }
        }

        if (networkAnimator != null && currentHealth.Value > 0)
        {
            networkAnimator.SetTrigger("Hit");
        }

        if (currentHealth.Value == 0)
        {
            ServerDie();
        }

        return true;
    }
    private void ServerDie()
    {
        if (!IsServer || dead.Value)
        {
            return;
        }

        dead.Value = true;

        if (networkAnimator != null)
        {
            networkAnimator.SetTrigger("Die");
        }

        if (enemyAI != null)
        {
            enemyAI.ServerStopForDeath();
        }

        ServerSpawnDrop();
        StartCoroutine(ServerDespawnRoutine());
    }

    private void ServerSpawnDrop()
    {
        if (pickupPrefab == null || dropItem == null)
        {
            return;
        }

        NetworkPickupItem pickup = Instantiate(
            pickupPrefab,
            transform.position + Vector3.up * 0.4f,
            Quaternion.identity);

        pickup.ServerInitialize(dropItem, dropAmount);
        pickup.NetworkObject.Spawn(true);
    }

    private IEnumerator ServerDespawnRoutine()
    {
        yield return new WaitForSeconds(despawnDelay);

        if (IsServer && IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void OnHealthChanged(int previous, int current)
    {
        ApplyHealthPresentation();

        // NetworkVariable从较高血量变成较低血量，说明服务器确认了伤害。
        int appliedDamage = Mathf.Max(0, previous - current);

        if (appliedDamage > 0)
        {
            CombatService.PublishNetworkDamage(
                this,
                appliedDamage);
        }
    }

    private void OnDeadChanged(bool previous, bool current)
    {
        ApplyDeadPresentation(current);
    }

    private void ApplyHealthPresentation()
    {
        if (presentationHealth != null)
        {
            presentationHealth.ApplyNetworkState(
                currentHealth.Value,
                maxHealth.Value);
        }
    }

    private void ApplyDeadPresentation(bool value)
    {
        if (combatColliders == null)
        {
            return;
        }

        foreach (Collider targetCollider in combatColliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = !value;
            }
        }
    }
}