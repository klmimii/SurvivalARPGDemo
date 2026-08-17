using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Playables;


public class EnemyController : MonoBehaviour
{
    [SerializeField]
    //Renderer渲染器是Unity中所有负责把物体画在屏幕上的组件的基类，renderer组件最核心的作用就是控制物体的材质和颜色
    //RnderersToFlash是用来存放怪物身上所有需要在受击时闪红的模型渲染组件
    private Renderer[] renderersToFlash;
    [SerializeField]
    //延迟销毁时间
    private float destoryDelay = 1.5f;

    private Health health;
    //敌人死后凋落物脚本
    private EnemyDropper dropper;
    //记录怪物是否死亡
    private bool isDead;
    //记录怪物原始的材质
    private Renderer[] rendererOriginal;
    //敌人身份
    private EnemyIdentity identity;

    private void Awake()
    {
        health = this.GetComponent<Health>();
        dropper = GetComponent<EnemyDropper>();
        rendererOriginal = renderersToFlash;
        identity = GetComponent<EnemyIdentity>();
    }

    private void OnEnable()
    {
        //在OnEnable中订阅死亡事件
        health.Died += Die;
        //订阅血量变化事件
        health.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        health.Died -= Die;
        health.HealthChanged -= OnHealthChanged;
    }

    private  void OnHealthChanged(int current,int max)
    {
        if(!isDead)
        {
            //开启受击变红协程
            StartCoroutine(FlashRed());
        }
    }

    private void Die()
    {
        if(isDead)
        {
            return;
        }
        isDead = true;
        //关闭已经死亡的怪物身上的碰撞器，防止玩家被怪物的尸体挡住和继续触发攻击判定
        this.GetComponent<Collider>().enabled = false;
        //掉落物品的逻辑交给了专门的EnemyDropper脚本
        dropper?.Drop();
        if (identity != null)
        {
            GameBootstrap.Events.PublishEnemyKilled(identity.EnemyId);
        }
        //开启延迟销毁协程
        StartCoroutine(DestroyAfterDelay());
    }

    /// <summary>
    /// 协程，在受击的时候把受击物体变成红色，必须使用Unity提供的StartCoroutine(FlashRed())来启动这个协程
    /// </summary>
    /// <returns></returns>
    private IEnumerator FlashRed()
    {
        //第一帧 立即执行
        //修改材质为红色达到受击效果。renderer.material会在内存中 自动克隆一份独有的Material副本，可能会造成内存泄漏
        //后面可以通过MateralPropertyBlock来修改颜色。
        foreach(Renderer targetRenderer in renderersToFlash)
        {
            targetRenderer.material.color = Color.red;
        }

        //暂停 告诉Unity把我挂起 0.08秒后在唤醒我
        yield return new WaitForSeconds(0.08f);

        //约0.08秒后的某一帧恢复执行
        //int index = 0;
        //闪红结束后回复颜色
        foreach(Renderer targetRenderer in renderersToFlash)
        {
            //Material material = rendererOriginal[index].material;
            if(targetRenderer!=null)
            {
                targetRenderer.material.color = Color.white;
                //闪红后恢复成默认材质
                //targetRenderer.material = material;
            }
        }
    }

    private IEnumerator DestroyAfterDelay()
    {
        //第一帧挂起，等待1.5秒
        yield return new WaitForSeconds(destoryDelay);
        //1.5秒后的某一帧，恢复执行，销毁GameObject
        Destroy(gameObject);
    }
}
