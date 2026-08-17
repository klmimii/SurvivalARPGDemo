using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcDialoguePresenter : MonoBehaviour
{
    [SerializeField] private NpcDialogueView dialogueView;
    [SerializeField] private ToastView toastView;

    private NpcQuestGiver activeNpc;

    private void OnEnable()
    {
        dialogueView.PrimaryClicked += OnPrimaryClicked;
    }

    private void OnDisable()
    {
        dialogueView.PrimaryClicked -= OnPrimaryClicked;
    }

    public void Open(NpcQuestGiver npc)
    {
        activeNpc = npc;
        Refresh();
    }

    private void Refresh()
    {
        QuestRuntime runtime = GameBootstrap.QuestService.GetQuest(activeNpc.Quest.questId);

        if (runtime == null)
        {
            dialogueView.Show(
                activeNpc.NpcName,
                activeNpc.Introduction,
                "接受任务",
                true);
            return;
        }

        if (runtime.Status == QuestStatus.Completed)
        {
            dialogueView.Show(
                activeNpc.NpcName,
                "你做得很好，营地终于有了火光。请收下奖励。",
                "交付任务并领取奖励",
                true);
            return;
        }

        if (runtime.Status == QuestStatus.Rewarded)
        {
            dialogueView.Show(
                activeNpc.NpcName,
                "营地已经建立。准备好后再去挑战森林深处吧。",
                string.Empty,
                false);
            return;
        }

        dialogueView.Show(
            activeNpc.NpcName,
            BuildProgressText(runtime),
            string.Empty,
            false);
    }

    private void OnPrimaryClicked()
    {
        if (activeNpc == null)
        {
            return;
        }

        QuestRuntime runtime = GameBootstrap.QuestService.GetQuest(activeNpc.Quest.questId);
        InventoryOperationResult result = runtime == null
            ? GameBootstrap.QuestService.TryAcceptQuest(activeNpc.Quest)
            : GameBootstrap.QuestService.TryClaimReward(activeNpc.Quest.questId);

        toastView.Show(result.Message);
        Refresh();
    }

    private string BuildProgressText(QuestRuntime runtime)
    {
        string text = "任务进度：\n";

        for (int i = 0; i < runtime.Definition.objectives.Length; i++)
        {
            QuestObjectiveDefinition objective = runtime.Definition.objectives[i];
            text += $"{objective.description} ({runtime.Progress[i]}/{objective.requiredCount})\n";
        }

        return text;
    }
}