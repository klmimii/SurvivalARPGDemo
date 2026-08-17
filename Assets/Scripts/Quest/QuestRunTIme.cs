using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 任务在运行时的动态状态实例
/// </summary>
[Serializable]
public class QuestRuntime
{
    public QuestDefinition Definition { get; }//只读，游戏任务
    public QuestStatus Status { get; private set; }//任务状态，动态变更，随玩家行为更新
    public int[] Progress { get; }//记录每个目标的当前完成数量

    public QuestRuntime(QuestDefinition definition)
    {
        Definition = definition;
        Status = QuestStatus.InProgress;//默认状态，进行中
        Progress = new int[definition.objectives.Length];//按照目标数量初始化进度数组
    }

    /// <summary>
    /// 增加进度方法
    /// </summary>
    /// <param name="objectiveIndex"></param>
    /// <param name="amount"></param>
    public void AddProgress(int objectiveIndex, int amount)
    {
        //如果任务不是进行中或者目标索引越界，直接拦截
        if (Status != QuestStatus.InProgress || objectiveIndex < 0 || objectiveIndex >= Progress.Length)
        {
            return;
        }

        //限制最大进度不超过配置中的requiredCount
        QuestObjectiveDefinition objective = Definition.objectives[objectiveIndex];
        Progress[objectiveIndex] = Math.Min(objective.requiredCount,Progress[objectiveIndex] + amount);

        //检查整个任务是否所有目标都已完成
        CheckCompleted();
    }

    /// <summary>
    /// 提供给UI或逻辑层调用的辅助函数。判断单个目标是否已完成
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public bool IsObjectiveCompleted(int index)
    {
        return Progress[index] >= Definition.objectives[index].requiredCount;
    }


    /// <summary>
    /// 标记为已领奖，当玩家在UI上点击领取奖励且发放完奖励后将状态从Completed切换成Rewarded
    /// </summary>
    public void MarkRewarded()
    {
        if (Status == QuestStatus.Completed)
        {
            Status = QuestStatus.Rewarded;
        }
    }

    /// <summary>
    /// 存档读条恢复
    /// </summary>
    /// <param name="status"></param>
    /// <param name="progress"></param>
    public void Restore(QuestStatus status, int[] progress)
    {
        Status = status;

        for (int i = 0; i < Progress.Length; i++)
        {
            Progress[i] = progress != null && i < progress.Length? Mathf.Clamp(progress[i], 0, Definition.objectives[i].requiredCount): 0;
        }
    }

    /// <summary>
    /// 检查整体完成状态
    /// </summary>
    private void CheckCompleted()
    {
        for (int i = 0; i < Progress.Length; i++)
        {
            //只要有一个目标没完成，任务就不算完成
            if (!IsObjectiveCompleted(i))
            {
                return;
            }
        }

        //所有目标均已完成，更新状态 
        Status = QuestStatus.Completed;
    }
}