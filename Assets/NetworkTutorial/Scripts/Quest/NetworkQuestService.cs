using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkPlayerInventory))]
public sealed class NetworkQuestService : NetworkBehaviour
{
    [Header("Server Quest Catalog")]
    [Tooltip("NPC 可以发放以及服务器能够识别的全部任务。")]
    [SerializeField]
    private QuestDefinition[] questCatalog;

    [Tooltip("进入世界后自动接取的任务。NPC 发放任务不要放这里。")]
    [SerializeField]
    private QuestDefinition[] autoAcceptedQuests;

    [Header("References On Same Player")]
    [SerializeField]
    private NetworkPlayerInventory inventory;

    private readonly Dictionary<string, QuestRuntime> serverQuests = new();
    private NetworkList<NetworkQuestStateData> ownerStates;
    private NetworkQuestOwnerFacade ownerFacade;
    private ToastView ownerToast;

    private void Awake()
    {
        // 每个玩家只能读取自己的任务状态。
        ownerStates = new NetworkList<NetworkQuestStateData>(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ServerInitializeAutoAccepted();
            ServerSyncAll();
        }

        if (IsOwner)
        {
            ownerToast = UnityEngine.Object.FindObjectOfType<ToastView>(true);
            ownerFacade = new NetworkQuestOwnerFacade(this, questCatalog);
            ownerStates.OnListChanged += OnOwnerStatesChanged;
            ownerFacade.RebuildFromNetwork();
            QuestServiceContext.BindNetwork(ownerFacade);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (ownerStates != null)
        {
            ownerStates.OnListChanged -= OnOwnerStatesChanged;
        }

        if (IsOwner && ownerFacade != null)
        {
            QuestServiceContext.UnbindNetwork(ownerFacade);
        }

        ownerFacade = null;
        serverQuests.Clear();
    }

    private void OnDestroy()
    {
        ownerStates?.Dispose();
    }

    public bool HasQuestDefinition(string questId)
    {
        return FindDefinition(questId) != null;
    }

    public void RequestAccept(string questId)
    {
        if (!IsOwner || !IsSpawned || string.IsNullOrWhiteSpace(questId))
        {
            return;
        }

        RequestAcceptServerRpc(new FixedString64Bytes(questId));
    }

    public void RequestClaimReward(string questId)
    {
        if (!IsOwner || !IsSpawned || string.IsNullOrWhiteSpace(questId))
        {
            return;
        }

        RequestClaimRewardServerRpc(new FixedString64Bytes(questId));
    }

    /// <summary>
    /// 只能由服务器上已经确认成功的玩法代码调用。
    /// </summary>
    public void ServerAddProgress(QuestObjectiveType type,string targetId,int amount)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(targetId) || amount <= 0)
        {
            return;
        }

        bool anyChanged = false;

        foreach (QuestRuntime runtime in serverQuests.Values)
        {
            if (runtime.Status != QuestStatus.InProgress)
            {
                continue;
            }

            QuestObjectiveDefinition[] objectives = runtime.Definition.objectives;

            for (int i = 0; i < objectives.Length; i++)
            {
                QuestObjectiveDefinition objective = objectives[i];
                if (objective.type != type || objective.targetId != targetId)
                {
                    continue;
                }

                int previous = runtime.Progress[i];
                runtime.AddProgress(i, amount);
                anyChanged |= runtime.Progress[i] != previous;
            }
        }

        if (anyChanged)
        {
            ServerSyncAll();
        }
    }

    [ServerRpc]
    private void RequestAcceptServerRpc(
        FixedString64Bytes questId,
        ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        string id = questId.ToString();
        QuestDefinition definition = FindDefinition(id);

        if (definition == null || definition.objectives == null ||
            definition.objectives.Length == 0)
        {
            ServerSendResult(senderId, "服务器任务不存在或没有配置目标。");
            return;
        }

        if (serverQuests.ContainsKey(id))
        {
            ServerSendResult(senderId, "任务已经接取。");
            return;
        }

        serverQuests.Add(id, new QuestRuntime(definition));
        ServerSyncAll();
        ServerSendResult(senderId, $"已接取任务：{definition.title}");
    }

    [ServerRpc]
    private void RequestClaimRewardServerRpc(
        FixedString64Bytes questId,
        ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        string id = questId.ToString();

        if (!serverQuests.TryGetValue(id, out QuestRuntime runtime) ||
            runtime.Status != QuestStatus.Completed)
        {
            ServerSendResult(senderId, "任务尚未完成或奖励已领取。");
            return;
        }

        if (inventory == null)
        {
            ServerSendResult(senderId, "服务器找不到该玩家背包。");
            return;
        }

        List<QuestReward> addedRewards = new();
        QuestReward[] rewards = runtime.Definition.rewards;

        if (rewards != null)
        {
            foreach (QuestReward reward in rewards)
            {
                if (reward == null || reward.item == null || reward.amount <= 0)
                {
                    continue;
                }

                if (!inventory.ServerTryAdd(reward.item, reward.amount))
                {
                    // 前面已经发出的奖励全部收回，任务仍保持 Completed。
                    foreach (QuestReward added in addedRewards)
                    {
                        inventory.ServerTryRemove(added.item, added.amount);
                    }

                    ServerSendResult(senderId, "背包空间不足，奖励未领取。");
                    return;
                }

                addedRewards.Add(reward);
            }
        }

        runtime.MarkRewarded();
        ServerSyncAll();
        ServerSendResult(senderId, $"已领取任务奖励：{runtime.Definition.title}");
    }

    public bool TryBuildOwnerRuntime(
        QuestDefinition definition,
        out QuestRuntime runtime)
    {
        runtime = null;

        if (!IsOwner || definition == null || definition.objectives == null)
        {
            return false;
        }

        int objectiveCount = definition.objectives.Length;
        int[] progress = new int[objectiveCount];
        bool found = false;
        QuestStatus status = QuestStatus.InProgress;

        for (int i = 0; i < ownerStates.Count; i++)
        {
            NetworkQuestStateData state = ownerStates[i];
            if (state.QuestId.ToString() != definition.questId)
            {
                continue;
            }

            found = true;
            status = (QuestStatus)Mathf.Clamp(
                state.Status,
                (int)QuestStatus.InProgress,
                (int)QuestStatus.Rewarded);

            if (state.ObjectiveIndex >= 0 &&
                state.ObjectiveIndex < progress.Length)
            {
                progress[state.ObjectiveIndex] = state.Progress;
            }
        }

        if (!found)
        {
            return false;
        }

        runtime = new QuestRuntime(definition);
        runtime.Restore(status, progress);
        return true;
    }

    private void ServerInitializeAutoAccepted()
    {
        serverQuests.Clear();

        if (autoAcceptedQuests == null)
        {
            return;
        }

        foreach (QuestDefinition definition in autoAcceptedQuests)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.questId) ||
                definition.objectives == null ||
                definition.objectives.Length == 0 ||
                FindDefinition(definition.questId) == null ||
                serverQuests.ContainsKey(definition.questId))
            {
                continue;
            }

            serverQuests.Add(
                definition.questId,
                new QuestRuntime(definition));
        }
    }

    private void ServerSyncAll()
    {
        if (!IsServer || questCatalog == null)
        {
            return;
        }

        ownerStates.Clear();

        foreach (QuestDefinition definition in questCatalog)
        {
            if (definition == null ||
                !serverQuests.TryGetValue(
                    definition.questId,
                    out QuestRuntime runtime))
            {
                continue;
            }

            // 正常任务至少配置一个目标。
            for (int i = 0; i < runtime.Progress.Length; i++)
            {
                ownerStates.Add(new NetworkQuestStateData(
                    definition.questId,
                    i,
                    runtime.Progress[i],
                    runtime.Status));
            }
        }
    }

    private QuestDefinition FindDefinition(string questId)
    {
        if (questCatalog == null || string.IsNullOrWhiteSpace(questId))
        {
            return null;
        }

        foreach (QuestDefinition definition in questCatalog)
        {
            if (definition != null && definition.questId == questId)
            {
                return definition;
            }
        }

        return null;
    }

    private void OnOwnerStatesChanged(
        NetworkListEvent<NetworkQuestStateData> changeEvent)
    {
        ownerFacade?.RebuildFromNetwork();
    }

    private void ServerSendResult(ulong targetClientId, string message)
    {
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };

        ShowResultClientRpc(new FixedString512Bytes(message), rpcParams);
    }

    [ClientRpc]
    private void ShowResultClientRpc(
        FixedString512Bytes message,
        ClientRpcParams rpcParams = default)
    {
        if (IsOwner && ownerToast != null)
        {
            ownerToast.Show(message.ToString());
        }
    }

    public void ServerCaptureQuests(List<QuestSaveData> destination)
    {
        if (!IsServer || destination == null)
        {
            return;
        }

        destination.Clear();

        foreach (QuestRuntime runtime in serverQuests.Values)
        {
            destination.Add(new QuestSaveData
            {
                questId = runtime.Definition.questId,
                status = runtime.Status,
                progress = (int[])runtime.Progress.Clone()
            });
        }
    }

    public void ServerRestoreQuests(
        IReadOnlyList<QuestSaveData> savedQuests)
    {
        if (!IsServer || savedQuests == null)
        {
            return;
        }

        serverQuests.Clear();

        foreach (QuestSaveData saved in savedQuests)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.questId))
            {
                continue;
            }

            QuestDefinition definition = FindDefinition(saved.questId);
            if (definition == null || definition.objectives == null ||
                definition.objectives.Length == 0)
            {
                Debug.LogWarning($"存档任务 ID 已失效：{saved.questId}", this);
                continue;
            }

            QuestRuntime runtime = new QuestRuntime(definition);
            runtime.Restore(saved.status, saved.progress);
            serverQuests[definition.questId] = runtime;
        }

        ServerSyncAll();
    }
}