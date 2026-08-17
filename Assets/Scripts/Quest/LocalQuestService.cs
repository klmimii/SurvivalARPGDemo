using System;
using System.Collections.Generic;

/// <summary>
/// 整个任务系统的核心大脑
/// </summary>
public class LocalQuestService : IQuestService
{
    private readonly GameEventHub events;
    private readonly IInventoryService inventoryService;
    private readonly List<QuestRuntime> quests = new List<QuestRuntime>();

    public IReadOnlyList<QuestRuntime> Quests => quests;
    public event Action Changed;

    //通过构造函数传入GameEventHub和IInventoryService避免了强耦合
    public LocalQuestService(GameEventHub eventHub, IInventoryService inventory)
    {
        events = eventHub;
        inventoryService = inventory;

        //订阅GameEventHub里的所有游戏事件
        events.ItemAdded += OnItemAdded;
        events.EnemyKilled += OnEnemyKilled;
        events.RecipeCrafted += OnRecipeCrafted;
        events.BuildingPlaced += OnBuildingPlaced;
        events.BossKilled += OnBossKilled;
    }

    /// <summary>
    /// 初始化方法，为游戏创建一套全新的运行时任务实例列表，并通知UI或其他系统进行更新
    /// </summary>
    /// <param name="definitions"></param>
    public void Initialize(QuestDefinition[] definitions)
    {
        //清空内存中的quests列表里的所有旧数据
        quests.Clear();

        foreach (QuestDefinition definition in definitions)
        {
            if (definition != null)
            {
                //将静态的配置资源转化为可变动的运行实例并加入到quests中
                quests.Add(new QuestRuntime(definition));
            }
        }
        //广播数据变更事件
        Changed?.Invoke();
    }

    /// <summary>
    /// 处理玩家点击领取任务奖励时的业务逻辑
    /// </summary>
    /// <param name="questId"></param>
    /// <returns></returns>
    public InventoryOperationResult TryClaimReward(string questId)
    {
        //在QuestRuntime中找到对应questId的任务 
        QuestRuntime quest = quests.Find(item => item.Definition.questId == questId);
        //如果传入了无效的ID，直接拦截
        if (quest == null)
        {
            return InventoryOperationResult.Fail("任务不存在。");
        }

        //检查任务状态，如果任务未完成或奖励已领取则直接返回
        if (quest.Status != QuestStatus.Completed)
        {
            return InventoryOperationResult.Fail("任务尚未完成或奖励已领取。");
        }

        // 先检查所有奖励是否有足够背包空间。原型只用逐项检查。
        foreach (QuestReward reward in quest.Definition.rewards)
        {
            if (!GameBootstrap.InventoryModel.CanAdd(reward.item, reward.amount))
            {
                return InventoryOperationResult.Fail("背包空间不足，无法领取奖励。");
            }
        }

        //有足够空间，则添加奖励进背包
        foreach (QuestReward reward in quest.Definition.rewards)
        {
            inventoryService.TryAddItem(reward.item, reward.amount);
        }

        //然后将任务状态切换为Rewarded放置玩家再次点击奖励
        quest.MarkRewarded();
        //让UI自动刷新
        Changed?.Invoke();
        //返回成功结果
        return InventoryOperationResult.Succeed("任务奖励已领取。");
    }

    /// <summary>
    /// 任务系统中专门用于存读档/恢复数据的关键方法
    /// </summary>
    /// <param name="questId"></param>
    /// <param name="status"></param>
    /// <param name="progress"></param>
    public void RestoreQuest(string questId, QuestStatus status, int[] progress)
    {
        //找到内存中的quests任务列表中对应ID的 任务
        QuestRuntime quest = quests.Find(item => item.Definition.questId == questId);
        if (quest == null)
        {
            return;
        }
        //将任务状态更新为存档里的状态，恢复进度
        quest.Restore(status, progress);
        //数据回复完毕后，主动广播事件
        Changed?.Invoke();
    }

    private void OnItemAdded(ItemDefinition item, int amount)
    {
        //将物品对象转换为类型+物品Id+获得数量
        AddProgress(QuestObjectiveType.ObtainItem, item.itemId, amount);
    }

    private void OnEnemyKilled(string enemyId)
    {
        //将杀怪事件转换为类型+配方ID+固定增加1次
        AddProgress(QuestObjectiveType.KillEnemy, enemyId, 1);
    }

    private void OnRecipeCrafted(RecipeDefinition recipe)
    {
        //将合成事件转换为类型加建筑ID加固定增加1次
        AddProgress(QuestObjectiveType.CraftRecipe, recipe.recipeId, 1);
    }

    private void OnBuildingPlaced(BuildingDefinition building)
    {
        // 将建造事件转换为：类型(BuildBuilding) + 建筑ID(building.buildingId) + 固定增加1次
        AddProgress(QuestObjectiveType.BuildBuilding, building.buildingId, 1);
    }

    private void OnBossKilled(string bossId)
    {
        // 将杀Boss事件转换为：类型(KillBoss) + BossID(bossId) + 固定增加1次
        AddProgress(QuestObjectiveType.KillBoss, bossId, 1);
    }

    /// <summary>
    /// 通用核心进度函数，无论游戏里触发了什么行为，最终都会统一汇聚到这里进行匹配处理
    /// </summary>
    /// <param name="type"></param>
    /// <param name="targetId"></param>
    /// <param name="amount"></param>
    private void AddProgress(QuestObjectiveType type, string targetId, int amount)
    {
        bool hasChanged = false;//标记本次事件是否真正改变了任何人物的进度

        //遍历玩家当前持有的所有任务
        foreach (QuestRuntime quest in quests)
        {
            //过滤，当玩家任务不是进行中则直接跳过
            if (quest.Status != QuestStatus.InProgress)
            {
                continue;
            }

            //遍历任务中的所有字母表
            for (int i = 0; i < quest.Definition.objectives.Length; i++)
            {
                QuestObjectiveDefinition objective = quest.Definition.objectives[i];
                if (objective.type == type && objective.targetId == targetId && !quest.IsObjectiveCompleted(i))
                {
                    //推进第i个目标的进度
                    quest.AddProgress(i, amount);
                    hasChanged = true;//记录有数据变动
                }
            }
        }

        //性能与UI优化，只有数据真正被修改了，才触发事件通知UI刷新
        if (hasChanged)
        {
            Changed?.Invoke();
        }
    }

    public QuestRuntime GetQuest(string questId)
    {
        return quests.Find(item => item.Definition.questId == questId);
    }

    public InventoryOperationResult TryAcceptQuest(QuestDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.questId))
        {
            return InventoryOperationResult.Fail("任务配置无效。");
        }

        if (GetQuest(definition.questId) != null)
        {
            return InventoryOperationResult.Fail("该任务已接取。\n");
        }

        quests.Add(new QuestRuntime(definition));
        Changed?.Invoke();
        return InventoryOperationResult.Succeed($"已接取任务：{definition.title}");
    }
}