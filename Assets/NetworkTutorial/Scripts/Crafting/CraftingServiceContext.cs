using System;

/// <summary>
/// 合成 UI 的运行时入口。
/// 单机时返回 GameBootstrap 的服务；联网本机玩家生成后切换到网络服务。
/// 它不是服务器，不存数据，只负责“现在 UI 应该问谁”。
/// </summary>
public static class CraftingServiceContext
{
    private static ICraftingService networkService;
    private static InventoryModel networkInventory;

    public static event Action BindingChanged;

    public static ICraftingService CurrentService =>
        networkService ?? GameBootstrap.CraftingService;

    public static InventoryModel CurrentInventory =>
        networkInventory ?? GameBootstrap.InventoryModel;

    public static void BindNetwork(
        ICraftingService service,
        InventoryModel inventory)
    {
        networkService = service;
        networkInventory = inventory;
        BindingChanged?.Invoke();
    }

    public static void UnbindNetwork(ICraftingService service)
    {
        // 防止旧玩家 Despawn 时清掉新玩家刚建立的绑定。
        if (!ReferenceEquals(networkService, service))
        {
            return;
        }

        networkService = null;
        networkInventory = null;
        BindingChanged?.Invoke();
    }
}