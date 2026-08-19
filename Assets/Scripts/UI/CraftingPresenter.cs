using UnityEngine;

public class CraftingPresenter : MonoBehaviour
{
    [SerializeField] private CraftingView craftingView;
    [SerializeField] private RecipeDefinition[] recipe;
    [SerializeField] private ToastView toastView;

    private InventoryModel boundInventory;

    private void Start()
    {
        CraftingServiceContext.BindingChanged += Rebind;
        Rebind();
    }

    private void OnDestroy()
    {
        CraftingServiceContext.BindingChanged -= Rebind;
        UnbindInventory();
    }

    private void Rebind()
    {
        UnbindInventory();
        boundInventory = CraftingServiceContext.CurrentInventory;

        if (boundInventory != null)
        {
            boundInventory.Changed += RefreshIfVisible;
        }

        RefreshIfVisible();
    }

    private void UnbindInventory()
    {
        if (boundInventory != null)
        {
            boundInventory.Changed -= RefreshIfVisible;
            boundInventory = null;
        }
    }

    public void Open()
    {
        craftingView.Show();
        Refresh();
    }

    public void Refresh()
    {
        ICraftingService service = CraftingServiceContext.CurrentService;

        if (service == null)
        {
            return;
        }

        craftingView.Render(recipe, service.CanCraft, TryCraft);
    }

    private void RefreshIfVisible()
    {
        if (craftingView != null &&
            craftingView.gameObject.activeInHierarchy)
        {
            Refresh();
        }
    }

    private void TryCraft(RecipeDefinition definition)
    {
        ICraftingService service = CraftingServiceContext.CurrentService;
        if (service == null)
        {
            toastView.Show("合成服务尚未准备好。");
            return;
        }

        CraftingResult result = service.TryCraft(definition);
        toastView.Show(result.Message);

        // 联网操作是异步的。这里先刷新一次；服务器同步背包后还会自动刷新。
        Refresh();
    }
}