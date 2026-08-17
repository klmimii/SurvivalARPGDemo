using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 仇恨列表的数据结构实现
/// </summary>
public class ThreatTable 
{
    //用字典存储仇恨数据，transform代表潜在的攻击目标（比如玩家角色、队友、宠物或召唤物的Transform坐标组件）；value代表这个目标积累的仇恨值数据
    private readonly Dictionary<Transform, float> threatValues = new Dictionary<Transform, float>();

    /// <summary>
    /// 增加仇恨
    /// </summary>
    /// <param name="target">目标对象，谁获得仇恨</param>
    /// <param name="amount">增加多少仇恨</param>
    public void AddThreat(Transform target,float amount)
    {
        //防御性检测，目标为空或者增加的仇恨值不合法时直接拦截
        if(target==null||amount<=0f)
        {
            return;
        }

        //尝试获取 target 当前已有的仇恨值（如果不存在则  currentThreat为默认值0f）
        //字典去查找target是否已在仇恨列表，如果找到了，返回true，如果没找到，方法返回false
        //如果找到了tryGetValue会把字典里存的那个仇恨值直接写进threatValue变量里，如果没找到TryValue会自动把currentThreat设置为float的默认值
        threatValues.TryGetValue(target, out float currentThreat);

        //增加仇恨值并更新字典
        threatValues[target] = currentThreat + amount;
    }

    /// <summary>
    /// 当某个目标死亡、脱离战斗范围、或者使用了隐身假死等清空仇恨技能时，从仇恨表中将其删除
    /// </summary>
    /// <param name="target"></param>
    public void Remove(Transform target )
    {
        if(target!=null)
        {
            threatValues.Remove(target);
        }
    }

    /// <summary>
    /// 怪物脱战回血、战斗彻底结束，或怪物自身死亡时，将整张仇恨表清空
    /// </summary>
    public  void Clear()
    {
        threatValues.Clear();
    }

    /// <summary>
    /// 获取最高仇恨目标
    /// </summary>
    /// <returns></returns>
    public Transform GetHighestThreatTarget()
    {
        //记录仇恨值最高的目标
        Transform bestTarget = null;
        //记录最高仇恨值数
        float hightestThreat = float.MinValue;

        foreach(KeyValuePair<Transform,float> pair in threatValues)
        {
            //当场景中玩家或召唤物被销毁后，C#引用还在，但Unity的UnityEngine.Object会假装他是null;
            //这个判空非常必要，因为它成功避免了由于目标对象中途被销毁而导致的报错或无效锁定
            if(pair.Key==null)
            {
                continue;
            }

            //记录仇恨值最高的目标及仇恨值
            if(pair.Value>hightestThreat)
            {
                hightestThreat = pair.Value;
                bestTarget = pair.Key;
            }
        }

        //返回仇恨值最高的对象
        return bestTarget;
    }


}
