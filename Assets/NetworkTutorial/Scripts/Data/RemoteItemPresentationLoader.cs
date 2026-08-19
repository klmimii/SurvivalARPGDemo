using UnityEngine;

public sealed class RemoteItemPresentationLoader : MonoBehaviour
{
    [SerializeField]
    private string configAddress =
        "config/remote-item-presentation";

    [SerializeField]
    private ToastView toastView;

    // 整个运行期间保留引用，使配置及其图标依赖不会被释放。
    private RemoteItemPresentationConfig loadedConfig;

    public async void LoadAndApply()
    {
        if (GameBootstrap.AssetService == null)
        {
            Debug.LogError("AssetService 尚未初始化。", this);
            return;
        }

        loadedConfig = await GameBootstrap.AssetService
            .LoadAsync<RemoteItemPresentationConfig>(configAddress);

        if (loadedConfig == null)
        {
            Debug.LogError(
                $"无法加载远程物品表现配置：{configAddress}",
                this);
            return;
        }

        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("场景中没有 ItemDatabase。", this);
            return;
        }

        int appliedCount = 0;

        if (loadedConfig.entries != null)
        {
            foreach (RemoteItemPresentationEntry entry in
                     loadedConfig.entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.itemId))
                {
                    continue;
                }

                ItemDefinition item =
                    ItemDatabase.Instance.GetById(entry.itemId);

                if (item == null)
                {
                    Debug.LogWarning(
                        $"远程配置找不到本地物品 ID：{entry.itemId}",
                        this);
                    continue;
                }

                if (entry.overrideDisplayName)
                {
                    item.displayName = entry.displayName;
                }

                if (entry.overrideDescription)
                {
                    item.description = entry.description;
                }

                if (entry.overrideIcon)
                {
                    item.icon = entry.icon;
                }

                appliedCount++;
            }
        }

        // 如果背包此刻已经打开，立即重新渲染；
        // 未打开时，下次 Open 本来也会读取新名称、描述和图标。
        InventoryPresenter presenter =
            Object.FindObjectOfType<InventoryPresenter>(true);

        presenter?.Refresh();

        Debug.Log(
            $"已应用远程物品表现配置 " +
            $"v{loadedConfig.contentVersion}，条目数：{appliedCount}",
            this);

        if (toastView != null &&
            !string.IsNullOrWhiteSpace(loadedConfig.updateNote))
        {
            toastView.Show(loadedConfig.updateNote);
        }
    }
}