using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 联网玩家的快捷药剂入口。
/// Owner 只发送“我要使用预制体配置的药剂”；
/// Host 检查血量和联网背包，再扣药、加血。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkPlayerInventory))]
[RequireComponent(typeof(NetworkPlayerState))]
public sealed class NetworkPlayerQuickUse : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField]
    private InputActionReference usePotionAction;

    [Header("Server Configuration")]
    [SerializeField]
    private ItemDefinition healingPotion;

    [Header("References On Same Player")]
    [SerializeField]
    private NetworkPlayerInventory inventory;

    [SerializeField]
    private NetworkPlayerState playerState;

    private ToastView ownerToast;
    private bool inputSubscribed;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        ownerToast = Object.FindObjectOfType<ToastView>(true);
        SubscribeInput();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeInput();
    }

    private void OnDestroy()
    {
        UnsubscribeInput();
    }

    /// <summary>
    /// 启用InputAction，然后吧OnUsePotion方法绑定到按键的performed事件上。
    /// </summary>
    private void SubscribeInput()
    {
        if (inputSubscribed || usePotionAction == null)
        {
            return;
        }

        usePotionAction.action.Enable();
        usePotionAction.action.performed += OnUsePotion;
        inputSubscribed = true;
    }

    /// <summary>
    /// 取消OnUsePotion的绑定，然后禁用InputAciton
    /// </summary>
    private void UnsubscribeInput()
    {
        if (!inputSubscribed || usePotionAction == null)
        {
            return;
        }

        usePotionAction.action.performed -= OnUsePotion;
        usePotionAction.action.Disable();
        inputSubscribed = false;
    }

    /// <summary>
    /// 本机玩家按下设定的按键时触发
    /// </summary>
    /// <param name="context"></param>
    private void OnUsePotion(InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned)
        {
            return;
        }

        // 打开背包、NPC 或建造菜单时不响应 H，避免输入穿透 UI。
        if (GameBootstrap.InputMode != null && !GameBootstrap.InputMode.IsGameplay())
        {
            return;
        }

        RequestUsePotionServerRpc();
    }

    /// <summary>
    /// 在服务器上执行，负责校验和处理使用药剂的完整逻辑
    /// </summary>
    /// <param name="rpcParams"></param>
    [ServerRpc]
    private void RequestUsePotionServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (healingPotion == null ||
            healingPotion.itemType != ItemType.Consumale ||
            healingPotion.healAmount <= 0)
        {
            ServerSendResult(senderId, "治愈药剂配置无效。");
            return;
        }

        if (inventory == null || playerState == null)
        {
            ServerSendResult(senderId, "服务器找不到玩家背包或血量组件。");
            return;
        }

        if (playerState.IsDead)
        {
            ServerSendResult(senderId, "死亡状态不能使用药剂。");
            return;
        }

        if (playerState.CurrentHealth >= playerState.MaxHealth)
        {
            ServerSendResult(senderId, "生命值已满。");
            return;
        }

        if (inventory.ServerCount(healingPotion) <= 0)
        {
            ServerSendResult(senderId, "背包中没有治愈药剂。");
            return;
        }

        int healthBefore = playerState.CurrentHealth;

        // 只有成功扣除网络背包中的药剂，才允许治疗。
        if (!inventory.ServerTryRemove(healingPotion, 1))
        {
            ServerSendResult(senderId, "药剂扣除失败，请重试。");
            return;
        }

        playerState.ServerHeal(healingPotion.healAmount);
        int actualHeal = playerState.CurrentHealth - healthBefore;

        if (actualHeal <= 0)
        {
            // 理论上不会发生；保险起见把药剂返还。
            inventory.ServerTryAdd(healingPotion, 1);
            ServerSendResult(senderId, "治疗失败，药剂已返还。");
            return;
        }

        ServerSendResult(senderId, $"使用{healingPotion.displayName}，恢复{actualHeal}点生命。");
    }

    /// <summary>
    /// 服务器上调用，	构造一个 定向 ClientRpc，只发给 targetClientId 对应的那个客户端，然后调用 ShowResultClientRpc
    /// </summary>
    /// <param name="targetClientId"></param>
    /// <param name="message"></param>
    private void ServerSendResult(ulong targetClientId, string message)
    {
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };

        ShowResultClientRpc(new FixedString512Bytes(message), rpcParams);
    }

    /// <summary>
    /// 所有客户端都会收到，但通过 rpcParams 限制了只有目标客户端会真正处理，判断 IsOwner，如果自己是本机玩家并且 ownerToast 不为空，就在屏幕上显示提示文字
    /// </summary>
    /// <param name="message"></param>
    /// <param name="rpcParams"></param>
    [ClientRpc]
    private void ShowResultClientRpc( FixedString512Bytes message,ClientRpcParams rpcParams = default)
    {
        if (IsOwner && ownerToast != null)
        {
            ownerToast.Show(message.ToString());
        }
    }
}