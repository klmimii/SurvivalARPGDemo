using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkPlayerInventory))]
public sealed class NetworkCraftingService :
    NetworkBehaviour,
    ICraftingService
{
    [Header("Server Recipe Whitelist")]
    [Tooltip("只有放进此数组的配方，服务器才允许合成。")]
    [SerializeField]
    private RecipeDefinition[] recipeCatalog;

    [Header("References On Same Player")]
    [SerializeField]
    private NetworkPlayerInventory inventory;

    [SerializeField]
    private NetworkQuestService questService;

    private ToastView ownerToast;
    private Coroutine bindRoutine;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        ownerToast = Object.FindObjectOfType<ToastView>(true);
        bindRoutine = StartCoroutine(BindOwnerWhenReady());
    }

    public override void OnNetworkDespawn()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        if (IsOwner)
        {
            CraftingServiceContext.UnbindNetwork(this);
        }
    }

    private IEnumerator BindOwnerWhenReady()
    {
        // NetworkBehaviour 的 OnNetworkSpawn 有组件顺序。
        // 等一帧也能避免 NetworkPlayerInventory 尚未创建 OwnerViewModel。
        while (IsSpawned &&
               (inventory == null || inventory.OwnerViewModel == null))
        {
            yield return null;
        }

        if (IsSpawned && IsOwner)
        {
            CraftingServiceContext.BindNetwork(
                this,
                inventory.OwnerViewModel);
        }

        bindRoutine = null;
    }

    public bool IsUnlocked(RecipeDefinition recipe)
    {
        InventoryModel model = inventory != null ? inventory.OwnerViewModel : null;

        if (!IsOwner || model == null || !HasRecipe(recipe))
        {
            return false;
        }

        return recipe.unlockItem == null || model.CountItem(recipe.unlockItem) > 0;
    }

    public bool CanCraft(RecipeDefinition recipe)
    {
        InventoryModel model = inventory != null ? inventory.OwnerViewModel : null;

        if (!IsOwner || model == null || !IsUnlocked(recipe) || recipe.outputItem == null || recipe.outputAmount <= 0)
        {
            return false;
        }

        Dictionary<ItemDefinition, int> costs = BuildIngredientAmounts(recipe);
        if (costs.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<ItemDefinition, int> pair in costs)
        {
            if (model.CountItem(pair.Key) < pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    public CraftingResult TryCraft(RecipeDefinition recipe)
    {
        if (!IsOwner || !IsSpawned)
        {
            return CraftingResult.Fail("当前没有可用的联网玩家。");
        }

        if (!CanCraft(recipe))
        {
            return CraftingResult.Fail("材料不足、配方未解锁或配方未加入服务器白名单。");
        }

        RequestCraftServerRpc(new FixedString64Bytes(recipe.recipeId));
        return CraftingResult.Succeed("已提交合成请求，等待服务器确认。");
    }

    [ServerRpc]
    private void RequestCraftServerRpc(
        FixedString64Bytes recipeId,
        ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        RecipeDefinition recipe = FindRecipe(recipeId.ToString());

        if (recipe == null || inventory == null ||
            recipe.outputItem == null || recipe.outputAmount <= 0)
        {
            ServerSendResult(senderId, "服务器找不到有效配方。");
            return;
        }

        if (recipe.unlockItem != null &&
            inventory.ServerCount(recipe.unlockItem) <= 0)
        {
            ServerSendResult(senderId, "尚未获得该配方图纸。");
            return;
        }

        Dictionary<ItemDefinition, int> costs = BuildIngredientAmounts(recipe);
        if (costs.Count == 0)
        {
            ServerSendResult(senderId, "该配方没有配置有效材料。");
            return;
        }

        foreach (KeyValuePair<ItemDefinition, int> pair in costs)
        {
            if (inventory.ServerCount(pair.Key) < pair.Value)
            {
                ServerSendResult(senderId, $"材料不足：{pair.Key.displayName}");
                return;
            }
        }

        // 预检查已经保证数量足够。记录已扣项目，方便任何异常时完整回滚。
        List<KeyValuePair<ItemDefinition, int>> removed = new();

        foreach (KeyValuePair<ItemDefinition, int> pair in costs)
        {
            if (!inventory.ServerTryRemove(pair.Key, pair.Value))
            {
                ServerRollback(removed);
                ServerSendResult(senderId, "扣除材料失败，已回滚。");
                return;
            }

            removed.Add(pair);
        }

        if (!inventory.ServerTryAdd(recipe.outputItem, recipe.outputAmount))
        {
            ServerRollback(removed);
            ServerSendResult(senderId, "背包没有空间，材料已返还。");
            return;
        }

        if (questService != null)
        {
            questService.ServerAddProgress(QuestObjectiveType.CraftRecipe, recipe.recipeId, 1);
        }

        ServerSendResult( senderId, $"合成成功：{recipe.outputItem.displayName} x{recipe.outputAmount}");
    }

    /// <summary>
    /// 回滚机制
    /// </summary>
    /// <param name="removed"></param>
    private void ServerRollback(List<KeyValuePair<ItemDefinition, int>> removed)
    {
        foreach (KeyValuePair<ItemDefinition, int> pair in removed)
        {
            inventory.ServerTryAdd(pair.Key, pair.Value);
        }
    }

    private Dictionary<ItemDefinition, int> BuildIngredientAmounts(
        RecipeDefinition recipe)
    {
        Dictionary<ItemDefinition, int> result = new();

        if (recipe == null || recipe.ingredients == null)
        {
            return result;
        }

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.item == null ||
                ingredient.amount <= 0)
            {
                continue;
            }

            result.TryGetValue(ingredient.item, out int current);
            result[ingredient.item] = current + ingredient.amount;
        }

        return result;
    }

    private bool HasRecipe(RecipeDefinition recipe)
    {
        return recipe != null &&
            !string.IsNullOrWhiteSpace(recipe.recipeId) &&
            FindRecipe(recipe.recipeId) != null;
    }

    private RecipeDefinition FindRecipe(string id)
    {
        if (recipeCatalog == null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (RecipeDefinition recipe in recipeCatalog)
        {
            if (recipe != null && recipe.recipeId == id)
            {
                return recipe;
            }
        }

        return null;
    }

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

    [ClientRpc]
    private void ShowResultClientRpc(
        FixedString512Bytes message,
        ClientRpcParams rpcParams = default)
    {
        if (IsOwner && ownerToast != null)
        {
            ownerToast.Show(message.ToString());
        }
    }
}