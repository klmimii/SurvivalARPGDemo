using System;
using System.Collections.Generic;

public sealed class NetworkQuestOwnerFacade : IQuestService
{
    private readonly NetworkQuestService networkService;
    private readonly QuestDefinition[] catalog;
    private readonly List<QuestRuntime> quests = new();

    public IReadOnlyList<QuestRuntime> Quests => quests;
    public event Action Changed;

    public NetworkQuestOwnerFacade(
        NetworkQuestService networkService,
        QuestDefinition[] catalog)
    {
        this.networkService = networkService;
        this.catalog = catalog ?? Array.Empty<QuestDefinition>();
    }

    public void RebuildFromNetwork()
    {
        quests.Clear();

        foreach (QuestDefinition definition in catalog)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.questId))
            {
                continue;
            }

            if (!networkService.TryBuildOwnerRuntime(
                    definition,
                    out QuestRuntime runtime))
            {
                continue;
            }

            quests.Add(runtime);
        }

        Changed?.Invoke();
    }

    public QuestRuntime GetQuest(string questId)
    {
        foreach (QuestRuntime quest in quests)
        {
            if (quest.Definition.questId == questId)
            {
                return quest;
            }
        }

        return null;
    }

    public InventoryOperationResult TryAcceptQuest(
        QuestDefinition definition)
    {
        if (definition == null ||
            !networkService.HasQuestDefinition(definition.questId))
        {
            return InventoryOperationResult.Fail(
                "该任务未加入服务器任务目录。");
        }

        if (GetQuest(definition.questId) != null)
        {
            return InventoryOperationResult.Fail("任务已经接取。");
        }

        networkService.RequestAccept(definition.questId);
        return InventoryOperationResult.Succeed(
            "已向服务器提交接取任务请求。");
    }

    public InventoryOperationResult TryClaimReward(string questId)
    {
        QuestRuntime runtime = GetQuest(questId);
        if (runtime == null || runtime.Status != QuestStatus.Completed)
        {
            return InventoryOperationResult.Fail("任务尚未完成。");
        }

        networkService.RequestClaimReward(questId);
        return InventoryOperationResult.Succeed(
            "已向服务器提交领取奖励请求。");
    }

    // 以下两个接口用于旧单机初始化/存档恢复。
    // 网络模式下真实状态只能由服务器创建和恢复，所以这里不执行写操作。
    public void Initialize(QuestDefinition[] definitions) { }

    public void RestoreQuest(
        string questId,
        QuestStatus status,
        int[] progress)
    { }
}