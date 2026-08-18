using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第5册验收专用：本机按 F6，请求服务器给自己的背包添加测试物品。
/// 第6册接入资源采集后可以禁用或删除此组件。
/// </summary>
public sealed class NetworkInventoryDebugInput : MonoBehaviour
{
    [SerializeField]
    private NetworkPlayerInventory inventory;

    [SerializeField]
    private ItemDefinition testItem;

    [Min(1)]
    [SerializeField]
    private int amount = 1;

    private void Update()
    {
        if (inventory == null ||
            !inventory.IsOwner ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f6Key.wasPressedThisFrame)
        {
            inventory.RequestDebugAdd(testItem, amount);
        }
    }
}