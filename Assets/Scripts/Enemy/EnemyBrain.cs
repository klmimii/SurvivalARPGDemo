using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人AI大脑（有限状态机）
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]//确保Enemy的GameObject同时挂载了这儿三个核心组件，如果没有挂载Unity编辑器会在挂载时自动补全
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EnemyCombat))]
public class EnemyBrain : MonoBehaviour
{
    //状态机枚举
    private enum State
    {
        Patrol,//巡逻，在指定的巡逻点之间来回走动
        Chase,//追击，发现最高仇恨目标，使用NavMeshAgent尝试追踪
        Attack,//攻击 进入攻击距离，停止移动，转向玩家并尝试攻击
        Return,//回归，丢失目标/超出范围后，放弃助记并返回出生点
        Dead//死亡，停止一切行为并禁用导航
    }

    [Header("Reference")]
    [SerializeField]
    private Transform[] patrolPoints;//巡逻点

    [Header("Perception")]//怪物感知参数
    [SerializeField]
    private float detectRange = 7f;//探测范围
    [SerializeField]
    private float loseTargetRange = 12f;//丢失目标范围
    [SerializeField]
    private float attackRange = 1.7f;//攻击范围

    [Header("Movement")]
    [SerializeField]
    private float patrolWaitSeconds = 1.2f;//巡逻等待时间
    [SerializeField]
    private float returnStopDistance = 0.25f;//返回停止距离

    [Header("Performance")]
    [Min(0.02f)]
    [SerializeField] private float thinkInterval = 0.15f;

    private float nextThinkTime;
    private float lastThinkTime;

    private NavMeshAgent agent;
    private Health health;
    private EnemyCombat enemyCombat;
    private ThreatTable threatTable;
    private Transform player;
    private Transform currentTarget;
    private Vector3 spawnPosition;//出生/刷新位置
    private State currentState;
    private int patrolIndex=0;
    private float patrolWaitTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        enemyCombat = GetComponent<EnemyCombat>();
        threatTable = new ThreatTable();
        spawnPosition = transform.position;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if(playerObject!=null)
        {
            player = playerObject.transform;
        }
    }

    private void OnEnable()
    {
        health.Died += OnDied;
    }

    private void Start()
    {
        ChangeState(State.Patrol);

        float stagger = Random.Range(0f, thinkInterval);
        nextThinkTime = Time.time + stagger;
        lastThinkTime = Time.time;
    }

    private void OnDisable()
    {
        health.Died -= OnDied;
    }

    private void Update()
    {
        if (currentState == State.Dead || player == null)
        {
            return;
        }

        // 攻击状态的面向需要逐帧更新，保证动画看起来平滑。
        UpdateAttackFacing();

        if (Time.time < nextThinkTime)
        {
            return;
        }

        float decisionDeltaTime = Mathf.Max(
            0.001f,
            Time.time - lastThinkTime);

        lastThinkTime = Time.time;
        nextThinkTime = Time.time + thinkInterval;

        UpdateThreatAndTarget(decisionDeltaTime);

        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol(decisionDeltaTime);
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Attack:
                UpdateAttack();
                break;
            case State.Return:
                UpdateReturn();
                break;
        }
    }

    //private void Update()
    //{
    //    //如果敌人已经处于Dead死亡状态或者场景里找不到player,直接退出函数，后续所有AI逻辑都不会再执行
    //    if (currentState == State.Dead || player == null)
    //    {
    //        return;
    //    }

    //    //不管敌人当前是在打盹巡逻还是在回血返回，每一帧都要先刷新一次周围的仇恨和目标状态。实时计算玩家是否进入了警戒范围，或者追击目标是否跑太远需要脱战并更新currentTarget
    //    UpdateThreatAndTarget();

    //    //典型的有限状态机分发器
    //    switch(currentState)
    //    {
    //        case State.Patrol:
    //            UpdatePatrol();//执行巡逻逻辑，走向下一个巡逻点
    //            break;
    //        case State.Chase:
    //            UpdateChase();//执行追击逻辑，用NavMesh追踪currentTarget
    //            break;
    //        case State.Attack:
    //            UpdateAttack(); //执行攻击逻辑：站定、面向玩家、尝试造成伤害
    //            break;
    //        case State.Return:
    //            UpdateReturn();//执行回归逻辑，走向spawnPosition
    //            break;
    //    }
    //}

    /// <summary>
    /// 感知和累加仇恨，离战与状态重置
    /// </summary>
    private void UpdateThreatAndTarget(float decisionDeltaTime)
    {
        //计算自己和玩家之间的距离
        float playerDistance = Vector3.Distance(this.transform.position, this.player.position);

        //如果距离小于探测距离，继续持续仇恨，当前数值只是原型参数，将时间累加作为仇恨值
        if(playerDistance<=detectRange)
        {
            threatTable.AddThreat(player, decisionDeltaTime);
        }

        //调用我们之前分析的threatTable仇恨表，找出当前仇恨值最高的那个Transform赋给currentTarget
        currentTarget = threatTable.GetHighestThreatTarget();
        //如果仇恨表为空，直接return结束本次判定
        if(currentTarget==null)
        {
            return;
        }

        //二次距离检测，这里算的是当前锁定目标的距离，而不是只算player。看当前警戒值最高的对象有没有进入脱战范围
        float targetDistance = Vector3.Distance(this.transform.position, currentTarget.position);
        //当目标距离超过了loseTargetRange，脱战距离
        if (targetDistance >loseTargetRange)
        {
            //从仇恨表中永久移除该目标
            threatTable.Remove(currentTarget);
            //将目标清空
            currentTarget = null;

            if (currentState != State.Return)
            {
                //如果敌人当前还没走在回出生点的路上，强制将其状态切为State.Return（回归状态）
                ChangeState(State.Return);
            }

        }
    }

    /// <summary>
    /// 负责控制敌人在闲置巡逻状态下的移动与路点循环
    /// </summary>
    private void UpdatePatrol(float decisionDeltaTime)
    {
        //1.如果发现了有攻击目标，立即切换到追击Chase状态，终止巡逻
        if (currentTarget != null)
        {
            ChangeState(State.Chase);
            return;
        }

        //2，如果没在Inspector里配置巡逻点数组，直接返回，防止报数组越界错误
        if(patrolPoints==null||patrolPoints.Length==0)
        {
            return;
        }

        //3.拿到当前巡逻点的坐标，让NavMeshAgent开始往那里走
        Transform destination = patrolPoints[patrolIndex];
        agent.SetDestination(destination.position);

        //4.到达判定，如果剩余距离大于停止距离，说明还在半路上，继续走，不往下执行
        if(agent.remainingDistance>agent.stoppingDistance)
        {
            return;
        }

        //5.驻留等待与路点切换，已经走到了巡逻点
        patrolWaitTimer += decisionDeltaTime;//开始原地倒计时
        if (patrolWaitTimer>=patrolWaitSeconds)
        {
            patrolWaitTimer = 0f;//重置计时器

            //取模运算%实现循环列表（比如0-1-2-0-1-2）
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }

    }
    
    private void UpdateChase()
    {
        //如果再追击过程中，目标突然没了，立刻打断追击，让敌人回归出生点
        if(currentTarget==null)
        {
            ChangeState(State.Return);
            return;
        }

        //如果当前目标与自己距离小于攻击范围，进入战斗
        float distance = Vector3.Distance(this.transform.position, currentTarget.position);
        if(distance<=attackRange)
        {
            ChangeState(State.Attack);
            return;
        }

        //不满足上面的两种情况，那就持续追击
        //确保NavMeshAgent处于开启移动状态，因为在之前的Attack状态或者打断逻辑里，Agent可能被设为isStopped=true
        agent.isStopped = false;
        agent.SetDestination(currentTarget.position);
    }

    private void UpdateAttack()
    {
        //如果目标丢失，立刻停止进攻，回到出生点
        if(currentTarget==null)
        {
            ChangeState(State.Return);
            return;
        }

        //站定，防止敌人边打边往玩家身体里挤
        agent.isStopped = true;
        //让怪物看向玩家，并把y轴置为0，防止玩家跳起来或站在高处时，怪物我会把整个身体往天上升起。抹平后怪物只会在水平地面上左右旋转
        //Vector3 lookDirection = currentTarget.position - this.transform.position;
        //lookDirection.y = 0;
        //if(lookDirection.sqrMagnitude>0.01f)
        //{
        //    //让怪物身体平滑转向玩家
        //    this.transform.rotation = Quaternion.Slerp(this.transform.rotation, Quaternion.LookRotation(lookDirection), 10f * Time.deltaTime);
        //}

        //实时计算玩家的距离，如果攻击期间距离变大，大于attackRange，立即切成State.Chase追击状态
        float distance = Vector3.Distance(transform.position, currentTarget.position);
        if (distance > attackRange)
        {
            ChangeState(State.Chase);
            return;
        }

        enemyCombat.TryAttack(currentTarget);
    }

    private void UpdateAttackFacing()
    {
        if (currentState != State.Attack || currentTarget == null)
        {
            return;
        }

        Vector3 lookDirection =
            currentTarget.position - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.01f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookDirection),
            10f * Time.deltaTime);
    }


    private void UpdateReturn()
    {
        //确保怪物回复行动能力
        agent.isStopped = false;
        agent.SetDestination(spawnPosition);
        //当距离小于等于returnStopDistance时，说明敌人已经回到了原地，重新开始巡逻
        if(Vector3.Distance(transform.position,spawnPosition)<=returnStopDistance)
        {
            ChangeState(State.Patrol);
        }
    }

    //如果不用ChangeState处理，只用currentState=nextState，情况1：敌人打完人后，玩家跑开，进入chase，但是没人帮他重置isStopped，仍然站在原地不动
    //情况二，敌人放弃追逐，但是因为仇恨值没擦除，下一帧又取追玩家了。
    //使用了ChangeState后，无论怎么频繁切换状态，移动刹车和仇恨数据永远会在切状态的第一时间被清理的明明白白的
    private void ChangeState(State nextState)
    {
        //将内部记录的currentState更新为传入的目标状态，后续Update里的switch就会立刻开始派发新状态的函数
        currentState = nextState;
        //只要下一个状态不是攻击状态，都强制吧agent.isStopped=false，重新解开
        if(nextState !=State.Attack)
        {
            agent.isStopped = false;
        }

        if(nextState==State.Return)
        {
            //进入回归状态后清空仇恨，不会走到一半又立即追原目标
            threatTable.Clear();
            currentTarget = null;
        }
    }

    private void OnDied()
    {
        currentState = State.Dead;
        agent.isStopped = true;
        agent.enabled = false;
        threatTable.Clear();
    }
}
