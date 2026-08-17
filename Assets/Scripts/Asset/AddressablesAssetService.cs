using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesAssetService : IAssetService
{
    private readonly Dictionary<Object, AsyncOperationHandle> handles = new Dictionary<Object, AsyncOperationHandle>();

    public async Task<T> LoadAsync<T>(string address) where T : Object
    {
        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
        T asset = await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"Addressables 加载失败：{address}");
            Addressables.Release(handle);
            return null;
        }

        handles[asset] = handle;
        return asset;
    }

    public void Release(Object asset)
    {
        if (asset != null && handles.TryGetValue(asset, out AsyncOperationHandle handle))
        {
            Addressables.Release(handle);
            handles.Remove(asset);
        }
    }
}