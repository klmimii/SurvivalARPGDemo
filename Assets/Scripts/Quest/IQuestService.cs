using System;
using System.Collections.Generic;

/// <summary>
/// 任务业务逻辑层
/// </summary>
public interface IQuestService
{
    IReadOnlyList<QuestRuntime> Quests { get; }//制度列表，暴露当前所有受管理的运行时对象
    event Action Changed;//当任务系统内部有任何数据发生变化时，触发
    void Initialize(QuestDefinition[] definitions);//传入所有任务的配置定义，用于在游戏启动、切换场景或加载玩家存档数据时初始化任务系统
    InventoryOperationResult TryClaimReward(string questId);//尝试领取奖励方法，因为可能领取失败，比如背包空间不足，所以返回一个背包操作结果类

    void RestoreQuest(string questId, QuestStatus status, int[] progress);//恢复单个任务

    QuestRuntime GetQuest(string questId);
    InventoryOperationResult TryAcceptQuest(QuestDefinition definition);
}