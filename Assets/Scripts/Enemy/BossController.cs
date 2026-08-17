using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossIdentity))]
public class BossController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private float detectRange = 18f;//探测范围
    [SerializeField] private float loseTargetRange = 25f;//丢失目标范围

    [Header("Phase 1")]
    [SerializeField] private int phaseOneDamage = 15;//阶段一伤害
    [SerializeField] private float phaseOneCooldown = 1.2f;//阶段一冷却
    [SerializeField] private float attackRange = 2.1f;//阶段一攻击距离

    [Header("Phase 2")]
    [SerializeField] private float phaseTwoHealthPercent = 0.5f;//阶段二触发阈值低于百分之多少 
    [SerializeField] private float phaseTwoMoveSpeed = 5f;//阶段二移动速度
    [SerializeField] private int phaseTwoDamage = 22;//阶段二伤害
    [SerializeField] private float phaseTwoCooldown = 0.75f;//阶段二冷却
    [SerializeField] private int slamDamage = 30;//冲击波伤害
    [SerializeField] private float slamRadius = 4f;//冲击波半径
    [SerializeField] private float slamCooldown = 5f;//冲击波冷却
    [SerializeField] private LayerMask playerLayer;//玩家层级

    [SerializeField] private BossAnimationController animationController;
    private bool pendingNormalAttack;
    private bool pendingSlam;

    private Health health;
    private NavMeshAgent agent;
    private BossIdentity identity;
    private Transform player;
    private bool phaseTwo;
    private bool dead;
    private float nextAttackTime;
    private float nextSlamTime;

    private float healthMultiplier = 1f;
    private float damageMultiplier = 1f;

    private void Awake()
    {
        //awake阶段获取组件
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        identity = GetComponent<BossIdentity>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void OnEnable()
    {
        health.HealthChanged += OnHealthChanged;//监听血量变化
        health.Died += OnDied;//监听死亡事件
    }

    private void OnDisable()
    {
        health.HealthChanged -= OnHealthChanged;
        health.Died -= OnDied;
    }

    private void Update()
    {
        //如果boss已经死了或者玩家为空，直接返回
        if (dead || player == null)
        {
            return;
        }
        //获取玩家血量，当玩家死了的时候停止寻路并原地待命
        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth == null || playerHealth.IsDead)
        {
            agent.isStopped = true;
            return;
        }

        //索敌与警戒判断
        float distance = Vector3.Distance(transform.position, player.position);
        //超出警戒和检测范围直接返回
        if (distance > loseTargetRange)
        {
            agent.isStopped = true;
            return;
        }

        if (distance > detectRange)
        {
            agent.isStopped = true;
            return;
        }

        //第二阶段技能优先判定，若进入Phase2且砸地CD好了，优先释放砸地
        if (phaseTwo && Time.time >= nextSlamTime)
        {
            PerformSlam();
            return;
        }

        //普攻与寻路判定
        if (distance <= attackRange)
        {
            AttackPlayer();//进入攻击距离，旋转面向并攻击玩家
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);//未达到攻击距离，追击玩家
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        //当血量降低到50%以下且尚未进入第二阶段时触发
        if (!phaseTwo && current <= max * phaseTwoHealthPercent)
        {
            EnterPhaseTwo();
        }
    }

    /// <summary>
    /// 进入第二阶段做什么
    /// </summary>
    private void EnterPhaseTwo()
    {
        phaseTwo = true;
        agent.speed = phaseTwoMoveSpeed;//提高寻路移动速度
        nextSlamTime = Time.time + 1f;//一秒后预热释放首次砸地
        Debug.Log("Boss 进入第二阶段：狂暴！");
        animationController.PlayEnrage();
    }

    /// <summary>
    /// 攻击玩家做什么
    /// </summary>
    private void AttackPlayer()
    {
        agent.isStopped = true;//攻击时原地站定 

        //平滑旋转玩家方向
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                10f * Time.deltaTime);
        }

        if (Time.time < nextAttackTime)//CD冷却置空
        {
            return;
        }

        //根据当前阶段对应伤害与CD数值
        int damage = phaseTwo ? phaseTwoDamage : phaseOneDamage;
        float cooldown = phaseTwo ? phaseTwoCooldown : phaseOneCooldown;
        nextAttackTime = Time.time + cooldown;
        pendingNormalAttack = true;
        animationController.PlayAttack();

    }

    /// <summary>
    /// 范围砸地技能
    /// </summary>
    private void PerformSlam()
    {
        nextSlamTime = Time.time + slamCooldown;//进入冷却
        agent.isStopped = true;

        //根据物理球形重叠检测获取范围内的玩家碰撞体
        //Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius, playerLayer);
        //foreach (Collider hit in hits)
        //{
        //    CombatService.TryDealDamage(hit, slamDamage);
        //}

        pendingSlam = true;
        animationController.PlaySlam();

        Debug.Log("Boss 使用范围砸地！");
    }

    /// <summary>
    /// boss死亡时做什么
    /// </summary>
    private void OnDied()
    {
        dead = true;
        agent.isStopped = true;
        agent.enabled = false;
        GameBootstrap.Events.PublishBossKilled(identity.BossId);//触发任务系统事件，发布Boss击杀广播
    }

    /// <summary>
    /// 可视化辅助绘图
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }

    public void AnimationEvent_NormalAttackHit()
    {
        if (!pendingNormalAttack || player == null)
        {
            return;
        }

        pendingNormalAttack = false;
        int damage = Mathf.RoundToInt((phaseTwo ? phaseTwoDamage : phaseOneDamage) * damageMultiplier);
        CombatService.TryDealDamage(player.GetComponent<Health>(), damage);
    }

    public void AnimationEvent_SlamHit()
    {
        if (!pendingSlam)
        {
            return;
        }

        pendingSlam = false;
        int damage = Mathf.RoundToInt(slamDamage * damageMultiplier);
        Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius, playerLayer);
        foreach (Collider hit in hits)
        {
            CombatService.TryDealDamage(hit, damage);
        }
    }

    public void ApplyRemoteBalance(float newHealthMultiplier, float newDamageMultiplier)
    {
        healthMultiplier = Mathf.Max(0.1f, newHealthMultiplier);
        damageMultiplier = Mathf.Max(0.1f, newDamageMultiplier);
    }
}