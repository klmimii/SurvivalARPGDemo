using System.Collections;
using UnityEngine;

public class GameStartupFlow : MonoBehaviour
{
    [SerializeField] private AddressablesUpdateService updateService;
    [SerializeField] private UpdateView updateView;
    [SerializeField] private RemoteBalanceLoader remoteBalanceLoader;
    [Header("After Startup")]
    [Tooltip("单机场景勾选；联机连接场景取消勾选")]
    [SerializeField] private bool enterGameplayAfterStartup = true;

    private IEnumerator Start()
    {
        // 启动页属于 UI：显示鼠标，禁止角色提前操作。
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);

        bool succeeded = false;
        yield return updateService.InitializeAndUpdate(result => succeeded = result);

        if (!succeeded)
        {
            // 第一版停留在错误信息；下一步加 RetryButton 调用 StartCoroutine 重试。
            yield break;
        }

        // 先完成 Catalog 更新，再加载远端配置；避免业务资源读取到旧 Catalog。
        remoteBalanceLoader.LoadAndApply();
        updateView.Hide();
        if (enterGameplayAfterStartup)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
        }
    }
}