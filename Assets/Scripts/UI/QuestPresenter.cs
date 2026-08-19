using UnityEngine;

public class QuestPresenter : MonoBehaviour
{
    [SerializeField] private QuestView questView;
    [SerializeField] private ToastView toastView;

    private IQuestService boundService;

    private void Start()
    {
        QuestServiceContext.BindingChanged += Rebind;
        Rebind();
    }

    private void OnDestroy()
    {
        QuestServiceContext.BindingChanged -= Rebind;
        Unbind();
    }

    private void Rebind()
    {
        Unbind();
        boundService = QuestServiceContext.Current;

        if (boundService != null)
        {
            boundService.Changed += RefreshIfVisible;
        }

        RefreshIfVisible();
    }

    private void Unbind()
    {
        if (boundService != null)
        {
            boundService.Changed -= RefreshIfVisible;
            boundService = null;
        }
    }

    public void Open()
    {
        questView.Show();
        Refresh();
    }

    public void Refresh()
    {
        IQuestService service = QuestServiceContext.Current;
        if (service != null)
        {
            questView.Render(service.Quests, ClaimReward);
        }
    }

    private void ClaimReward(string questId)
    {
        IQuestService service = QuestServiceContext.Current;
        if (service == null)
        {
            return;
        }

        InventoryOperationResult result = service.TryClaimReward(questId);
        toastView.Show(result.Message);
        Refresh();
    }

    private void RefreshIfVisible()
    {
        if (questView != null && questView.gameObject.activeInHierarchy)
        {
            Refresh();
        }
    }
}