using UnityEngine;

public class NpcDialoguePresenter : MonoBehaviour
{
    [SerializeField] private NpcDialogueView dialogueView;
    [SerializeField] private ToastView toastView;

    private NpcQuestGiver activeNpc;
    private IQuestService boundService;

    private void OnEnable()
    {
        dialogueView.PrimaryClicked += OnPrimaryClicked;
        QuestServiceContext.BindingChanged += Rebind;
        Rebind();
    }

    private void OnDisable()
    {
        dialogueView.PrimaryClicked -= OnPrimaryClicked;
        QuestServiceContext.BindingChanged -= Rebind;
        Unbind();
    }

    private void Rebind()
    {
        Unbind();
        boundService = QuestServiceContext.Current;

        if (boundService != null)
        {
            boundService.Changed += OnQuestChanged;
        }

        OnQuestChanged();
    }

    private void Unbind()
    {
        if (boundService != null)
        {
            boundService.Changed -= OnQuestChanged;
            boundService = null;
        }
    }

    private void OnQuestChanged()
    {
        if (activeNpc != null && dialogueView.gameObject.activeInHierarchy)
        {
            Refresh();
        }
    }

    public void Open(NpcQuestGiver npc)
    {
        activeNpc = npc;
        Refresh();
    }

    private void Refresh()
    {
        IQuestService service = QuestServiceContext.Current;
        if (activeNpc == null || service == null)
        {
            return;
        }

        QuestRuntime runtime = service.GetQuest(activeNpc.Quest.questId);

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
                "任务已经完成，请领取奖励。",
                "交付任务并领取奖励",
                true);
            return;
        }

        if (runtime.Status == QuestStatus.Rewarded)
        {
            dialogueView.Show(
                activeNpc.NpcName,
                "这个任务已经完成。",
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
        IQuestService service = QuestServiceContext.Current;
        if (activeNpc == null || service == null)
        {
            return;
        }

        QuestRuntime runtime = service.GetQuest(activeNpc.Quest.questId);
        InventoryOperationResult result = runtime == null
            ? service.TryAcceptQuest(activeNpc.Quest)
            : service.TryClaimReward(activeNpc.Quest.questId);

        toastView.Show(result.Message);
        // ServerRpc 是异步的。服务器同步状态后 OnQuestChanged 会再次刷新。
        Refresh();
    }

    private string BuildProgressText(QuestRuntime runtime)
    {
        string text = "任务进度：\n";

        for (int i = 0; i < runtime.Definition.objectives.Length; i++)
        {
            QuestObjectiveDefinition objective =
                runtime.Definition.objectives[i];

            text += $"{objective.description} " +
                $"({runtime.Progress[i]}/{objective.requiredCount})\n";
        }

        return text;
    }
}