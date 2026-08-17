using UnityEngine;

public class RemoteBalanceLoader : MonoBehaviour
{
    [SerializeField] private BossController bossController;
    [SerializeField] private ToastView toastView;

    // 只能在 AddressablesUpdateService 完成 Catalog 更新后调用。

    private void Start()
    {
        LoadAndApply();
    }


    public async void LoadAndApply()
    {
        RemoteBalanceConfig config = await GameBootstrap.AssetService
            .LoadAsync<RemoteBalanceConfig>("config/remote-balance");

        if (config == null)
        {
            return;
        }

        bossController.ApplyRemoteBalance(
            config.bossHealthMultiplier,
            config.bossDamageMultiplier);

        if (!string.IsNullOrEmpty(config.updateNote))
        {
            toastView.Show(config.updateNote);
        }
    }
}