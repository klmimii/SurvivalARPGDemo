using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class NetworkEnemyAI : NetworkBehaviour
{
    private enum State
    {
        Patrol,
        Chase,
        Attack,
        Return,
        Dead
    }

    [Header("References")]
    [SerializeField]
    private NavMeshAgent agent;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private NetworkAnimator networkAnimator;

    [Header("Patrol")]
    [Tooltip("拖入场景中不会跟随怪物移动的巡逻点。")]
    [SerializeField]
    private Transform[] patrolPoints;

    [Min(0f)]
    [SerializeField]
    private float patrolWaitSeconds = 1.2f;

    [Min(0.05f)]
    [SerializeField]
    private float patrolArriveDistance = 0.25f;

    [Header("Perception")]
    [Min(0.5f)]
    [SerializeField]
    private float detectRange = 7f;

    [Min(0.5f)]
    [SerializeField]
    private float loseTargetRange = 12f;

    [Min(0.5f)]
    [SerializeField]
    private float attackRange = 1.7f;

    [Header("Attack")]
    [Min(1)]
    [SerializeField]
    private int damage = 10;

    [Min(0.1f)]
    [SerializeField]
    private float attackCooldown = 1.2f;

    [Tooltip("攻击动画即使没触发Finished事件，超过该时间也会自动解锁。")]
    [Min(0.2f)]
    [SerializeField]
    private float attackAnimationTimeout = 2f;

    [Header("Return")]
    [Min(0.05f)]
    [SerializeField]
    private float returnStopDistance = 0.3f;

    [Header("Performance")]
    [Min(0.05f)]
    [SerializeField]
    private float thinkInterval = 0.15f;

    private readonly ThreatTable threatTable = new ThreatTable();

    private State state;
    private Transform currentTarget;
    private Transform pendingAttackTarget;
    private Vector3 spawnPosition;

    private int patrolIndex;
    private bool waitingAtPatrolPoint;
    private float patrolWaitUntil;

    private float nextThinkTime;
    private float nextAttackTime;
    private float pendingAttackReleaseTime;

    private static readonly int Speed = Animator.StringToHash("Speed");

    public override void OnNetworkSpawn()
    {
        spawnPosition = transform.position;

        if (!IsServer)
        {
            // 客户端只接收NetworkTransform结果，不能自己寻路。
            if (agent != null)
            {
                agent.enabled = false;
            }

            return;
        }

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        state = State.Patrol;
        patrolIndex = 0;
        waitingAtPatrolPoint = false;
        nextThinkTime = Time.time + Random.Range(0f, thinkInterval);
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || state == State.Dead)
        {
            return;
        }

        UpdateAnimatorSpeed();

        // 动画事件丢失时的保险，防止攻击永久锁死。
        if (pendingAttackTarget != null &&
            Time.time >= pendingAttackReleaseTime)
        {
            ClearPendingAttack();
        }

        if (state == State.Attack)
        {
            UpdateAttackFacing();
        }

        if (Time.time < nextThinkTime)
        {
            return;
        }

        nextThinkTime = Time.time + thinkInterval;
        ServerRefreshThreatTargets();
        ServerUpdateState();
    }

    public void ServerAddThreat(Transform target, float amount)
    {
        if (IsServer && target != null && amount > 0f)
        {
            threatTable.AddThreat(target, amount);
        }
    }

    /// <summary>
    /// 怪物的Attack被Hit动画打断时调用。
    /// 不清除仇恨，只解除上一次攻击的动画锁。
    /// </summary>
    public void ServerInterruptAttack()
    {
        if (!IsServer)
        {
            return;
        }

        ClearPendingAttack();
    }

    public void ServerStopForDeath()
    {
        if (!IsServer)
        {
            return;
        }

        state = State.Dead;
        currentTarget = null;
        ClearPendingAttack();
        threatTable.Clear();

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (animator != null)
        {
            animator.SetFloat(Speed, 0f);
        }
    }

    private void ServerRefreshThreatTargets()
    {
        foreach (NetworkClient client in
                 NetworkManager.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null)
            {
                continue;
            }

            NetworkPlayerState playerState =
                playerObject.GetComponent<NetworkPlayerState>();

            if (playerState == null || playerState.IsDead)
            {
                threatTable.Remove(playerObject.transform);
                continue;
            }

            float distance = Vector3.Distance(
                transform.position,
                playerObject.transform.position);

            if (distance <= detectRange)
            {
                threatTable.AddThreat(
                    playerObject.transform,
                    thinkInterval);
            }
        }

        currentTarget = threatTable.GetHighestThreatTarget();

        if (currentTarget != null &&
            Vector3.Distance(transform.position, currentTarget.position) >
            loseTargetRange)
        {
            threatTable.Remove(currentTarget);
            currentTarget = null;
        }
    }

    private void ServerUpdateState()
    {
        if (currentTarget == null)
        {
            ServerUpdateWithoutTarget();
            return;
        }

        waitingAtPatrolPoint = false;

        float targetDistance = Vector3.Distance(
            transform.position,
            currentTarget.position);

        if (targetDistance > attackRange)
        {
            state = State.Chase;
            agent.isStopped = false;
            agent.SetDestination(currentTarget.position);
            return;
        }

        state = State.Attack;
        agent.isStopped = true;

        if (pendingAttackTarget == null &&
            Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            pendingAttackTarget = currentTarget;
            pendingAttackReleaseTime =
                Time.time + attackAnimationTimeout;

            if (networkAnimator != null)
            {
                networkAnimator.SetTrigger("Attack");
            }
        }
    }

    private void ServerUpdateWithoutTarget()
    {
        // 追击或攻击途中丢失目标，先返回出生位置。
        if (state == State.Chase || state == State.Attack)
        {
            state = State.Return;
            ClearPendingAttack();
            waitingAtPatrolPoint = false;
        }

        if (state == State.Return)
        {
            float homeDistance =
                Vector3.Distance(transform.position, spawnPosition);

            if (homeDistance > returnStopDistance)
            {
                agent.isStopped = false;
                agent.SetDestination(spawnPosition);
                return;
            }

            agent.ResetPath();
            state = State.Patrol;
            patrolIndex = 0;
            waitingAtPatrolPoint = false;
        }

        ServerUpdatePatrol();
    }

    private void ServerUpdatePatrol()
    {
        state = State.Patrol;

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.isStopped = true;
            return;
        }

        if (patrolIndex < 0 || patrolIndex >= patrolPoints.Length)
        {
            patrolIndex = 0;
        }

        Transform point = patrolPoints[patrolIndex];
        if (point == null)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            return;
        }

        if (waitingAtPatrolPoint)
        {
            agent.isStopped = true;

            if (Time.time < patrolWaitUntil)
            {
                return;
            }

            waitingAtPatrolPoint = false;
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            point = patrolPoints[patrolIndex];

            if (point == null)
            {
                return;
            }
        }

        agent.isStopped = false;
        agent.SetDestination(point.position);

        if (!agent.pathPending &&
            agent.remainingDistance <= Mathf.Max(
                patrolArriveDistance,
                agent.stoppingDistance))
        {
            agent.isStopped = true;
            agent.ResetPath();
            waitingAtPatrolPoint = true;
            patrolWaitUntil = Time.time + patrolWaitSeconds;
        }
    }

    private void UpdateAttackFacing()
    {
        if (currentTarget == null)
        {
            return;
        }

        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction.normalized),
                10f * Time.deltaTime);
        }
    }

    private void UpdateAnimatorSpeed()
    {
        if (animator == null)
        {
            return;
        }

        float normalizedSpeed =
            agent != null && agent.enabled
                ? agent.velocity.magnitude /
                  Mathf.Max(0.01f, agent.speed)
                : 0f;

        animator.SetFloat(
            Speed,
            normalizedSpeed,
            0.1f,
            Time.deltaTime);
    }

    public void AnimationEvent_AttackHit()
    {
        if (!IsServer || pendingAttackTarget == null)
        {
            return;
        }

        if (Vector3.Distance(
                transform.position,
                pendingAttackTarget.position) > attackRange + 0.5f)
        {
            return;
        }

        NetworkPlayerState playerState =
            pendingAttackTarget.GetComponentInParent<NetworkPlayerState>();

        if (playerState != null && !playerState.IsDead)
        {
            playerState.ServerTakeDamage(damage);
        }
    }

    public void AnimationEvent_AttackFinished()
    {
        if (IsServer)
        {
            ClearPendingAttack();
        }
    }

    private void ClearPendingAttack()
    {
        pendingAttackTarget = null;
        pendingAttackReleaseTime = 0f;
    }
}