using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform attackPoint;//攻击点位置，是攻击判定的圆心位置
    [SerializeField]
    private InputActionReference attackAction;//攻击按键绑定

    [Header("Attack Settings")]
    [SerializeField]
    private float attackRadius = 1.1f;//攻击半径
    [SerializeField]
    private int damage = 20;//单次攻击造成的伤害数值
    [SerializeField]
    private float attackCooldown = 0.45f;//攻击冷却时间，两次攻击间隔的时间必须是多久，防止连点无限挥拳
    [SerializeField]
    private LayerMask targetLayers;//目标层级过滤器，比如只检测enemy，不会打到自己地面等等

    private float nextAttackTime;//内部冷却计时，用来记录下一次什么时候发起攻击

    /// <summary>
    /// 是否允许攻击开关，默认为true
    /// 外部如建造系统可以将其设为false来禁用攻击
    /// </summary>
    //public bool CanAttack { get; set; } = true;

    ////提供外部调用的方法
    //public void SetCanAttack(bool enable)
    //{
    //    CanAttack = enable;
    //}

    private void OnEnable()
    {
        attackAction.action.Enable();
        //performed是一个回调事件。InputAction有三个回调事件，started（按键刚刚按下瞬间触发1次）performed(按键按下，达到触发阈值执行)canceled（按键松开时候触发）
        //preformed 可以设置按住一段时间后才执行performed.不适合走路，适合瞬时触发操作，而移动属于持续状态
        attackAction.action.performed += OnAttackPerformed;//订阅按下事件
    }

    private void OnDisable()
    {
        attackAction.action.performed -= OnAttackPerformed;//及时取消订阅
        attackAction.action.Disable();
    }

    //攻击按键回调函数
    //context是回调上下文，如果作为performed的函数则强制需要传入。他携带了本次触发事件的全部信息（ReadValue这次输入的值，action这次毁掉的InoutAction对象，phase判断当前是start performed canceled,duration按键按住了多久，可以做长按短按区分攻击，control知道是哪个设备出发的鼠标手柄键盘）
    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        //如果现在不是游戏模式，直接返回，不执行后面的逻辑
        if(GameBootstrap.InputMode!=null&&!GameBootstrap.InputMode.IsGameplay())
        {
            return;
        }
        //如果处于不能攻击状态，直接返回
        //if(!CanAttack)
        //{
        //    return;
        //}
        if(Time.time<nextAttackTime)
        {
            return;
        }
        nextAttackTime = Time.time + attackCooldown;
        Attack();
    } 

    private void Attack()
    {
        //球形范围物理重叠检测
        Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackRadius, targetLayers);

        //一个敌人可能有多个Collider，所以要防止同一次攻击重复扣血.如果没有去重，foreach循环就会执行两次
        //HashSet查找效率为o1的特性，hashSet不允许存放重复元素，记录当前这一刀已经伤害过的对象
        HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

        foreach(Collider hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if(damageable==null||hitTargets.Contains(damageable))
            {
                //砍中第一个碰撞体，找到enemy的IDamageable，加入hitTargets并造成20点伤害，
                //砍中第二个碰撞体，找到同一个Enemy的IDamageable，直接跳过
                //continue立即种植这一轮的foreach循环，直接跳到下一次循环开始，后续代码不再执行
                continue;
            }
            hitTargets.Add(damageable);
            //CombatService是静态类，不用实例化，直接调用类名即可。
            CombatService.TryDealDamage(hit, damage);
        }
    }

    //编辑器辅助线框绘制,这个函数是Unity内置的消息回调函数，不需要手动调用 Unity引擎自动执行
    //当在Unity编辑器中选中player时，会自动在attackPoint的位置绘制出一个红色的线框球体
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}
