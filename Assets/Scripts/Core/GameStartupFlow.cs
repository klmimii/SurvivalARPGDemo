using System.Collections;
using UnityEngine;

public class GameStartupFlow : MonoBehaviour
{
    [SerializeField] private AddressablesUpdateService updateService;
    [SerializeField] private UpdateView updateView;
    [SerializeField]
    private RemoteItemPresentationLoader remoteItemPresentationLoader;
    [Header("After Startup")]
    [Tooltip("单机场景勾选；联机连接场景取消勾选")]
    [SerializeField] private bool enterGameplayAfterStartup = true;

    private IEnumerator Start()
    {
        Debug.Log("[热更新] 启动流程开始", this);

        // 即使场景中误关了面板，也强制显示
        updateView.Show();
        updateView.SetStatus("准备检查资源更新...");
        updateView.SetProgress(0f);

        // 先显示一帧，避免面板还没有渲染就进入异步检查
        yield return null;

        GameBootstrap.InputMode.SetMode(GameInputMode.UI);

        bool succeeded = false;
        yield return updateService.InitializeAndUpdate(
            result => succeeded = result);

        if (!succeeded)
        {
            Debug.LogError("[热更新] 资源更新失败", this);
            yield break;
        }

        Debug.Log("[热更新] Addressables 资源准备完成", this);

        remoteItemPresentationLoader.LoadAndApply();

        // 至少显示一小段时间，方便演示时看清结果
        yield return new WaitForSecondsRealtime(0.8f);

        updateView.Hide();

        if (enterGameplayAfterStartup)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
        }
    }
}