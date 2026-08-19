using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesUpdateService : MonoBehaviour
{
    [SerializeField] private UpdateView updateView;
    [SerializeField] private string preloadLabel = "preload";

    public IEnumerator InitializeAndUpdate(Action<bool> completed)
    {
        updateView.SetStatus("正在初始化资源系统...");
        updateView.SetProgress(0f);

        // false 表示不要自动释放，后面由我们手动释放
        AsyncOperationHandle initializeHandle =
            Addressables.InitializeAsync(false);

        yield return initializeHandle;

        if (initializeHandle.Status != AsyncOperationStatus.Succeeded)
        {
            updateView.SetStatus("资源系统初始化失败。");
            Debug.LogError("[热更新] Addressables 初始化失败", this);

            Addressables.Release(initializeHandle);

            completed?.Invoke(false);
            yield break;
        }

        // 使用结束后手动释放
        Addressables.Release(initializeHandle);

        updateView.SetStatus("正在检查资源更新...");
        AsyncOperationHandle<List<string>> checkHandle = Addressables.CheckForCatalogUpdates(false);
        yield return checkHandle;

        if (checkHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(checkHandle);
            updateView.SetStatus("检查资源更新失败，请检查网络。");
            completed?.Invoke(false);
            yield break;
        }

        List<string> catalogs = checkHandle.Result;
        if (catalogs != null && catalogs.Count > 0)
        {
            updateView.SetStatus("正在更新资源目录...");
            AsyncOperationHandle<List<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator>> updateHandle = Addressables.UpdateCatalogs(true, catalogs, false);
            yield return updateHandle;

            if (updateHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(updateHandle);
                Addressables.Release(checkHandle);
                updateView.SetStatus("资源目录更新失败。");
                completed?.Invoke(false);
                yield break;
            }

            Addressables.Release(updateHandle);
        }

        Addressables.Release(checkHandle);

        updateView.SetStatus("正在计算下载大小...");
        AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(preloadLabel);
        yield return sizeHandle;

        if (sizeHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(sizeHandle);
            updateView.SetStatus("无法获取下载大小。");
            completed?.Invoke(false);
            yield break;
        }

        long downloadBytes = sizeHandle.Result;
        Addressables.Release(sizeHandle);

        if (downloadBytes > 0)
        {
            updateView.SetStatus($"正在下载资源（{FormatBytes(downloadBytes)}）...");
            AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync(preloadLabel, false);

            while (!downloadHandle.IsDone)
            {
                // PercentComplete 代表子操作完成比例；用于演示进度足够。
                updateView.SetProgress(downloadHandle.PercentComplete);
                yield return null;
            }

            if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(downloadHandle);
                updateView.SetStatus("资源下载失败，请重试。");
                completed?.Invoke(false);
                yield break;
            }

            Addressables.Release(downloadHandle);
        }

        updateView.SetProgress(1f);
        updateView.SetStatus("资源已准备完成。");
        completed?.Invoke(true);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024f:F1} KB";
        }

        return $"{bytes / 1024f / 1024f:F1} MB";
    }
}