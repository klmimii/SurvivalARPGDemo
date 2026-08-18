using System.Collections.Generic;
using UnityEngine;

public class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private InventoryView inventoryView;

    private InventoryModel inventoryModel;

    // 当前左侧选择的分类。
    private ItemCategory currentCategory = ItemCategory.All;

    // 当前点击的物品，用于右侧详情和选中框。
    private ItemDefinition selectedDefinition;

    //private void Start()
    //{
    //    inventoryModel = GameBootstrap.InventoryModel;

    //    if (inventoryModel == null)
    //    {
    //        Debug.LogError(
    //            "InventoryPresenter找不到InventoryModel，请确认GameBootstrap已启用。",
    //            this);
    //        return;
    //    }

    //    inventoryModel.Changed += Refresh;
    //    inventoryView.CategorySelected += ChangeCategory;

    //    Refresh();
    //}

    //private void OnDestroy()
    //{
    //    if (inventoryModel != null)
    //    {
    //        inventoryModel.Changed -= Refresh;
    //    }

    //    if (inventoryView != null)
    //    {
    //        inventoryView.CategorySelected -= ChangeCategory;
    //    }
    //}
    private bool viewEventsBound;

    private void Start()
    {
        BindViewEventsOnce();

        // 单机场景继续使用 GameBootstrap.InventoryModel；
        // 网络场景若已提前 Bind，则不覆盖网络镜像。
        if (inventoryModel == null)
        {
            Bind(GameBootstrap.InventoryModel);
        }
        else
        {
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (inventoryModel != null)
        {
            inventoryModel.Changed -= Refresh;
        }

        if (inventoryView != null && viewEventsBound)
        {
            inventoryView.CategorySelected -= ChangeCategory;
        }
    }

    public void Bind(InventoryModel model)
    {
        if (inventoryModel != null)
        {
            inventoryModel.Changed -= Refresh;
        }

        inventoryModel = model;

        if (inventoryModel == null)
        {
            Debug.LogWarning(
                "InventoryPresenter 收到空的 InventoryModel。",
                this);
            return;
        }

        inventoryModel.Changed -= Refresh;
        inventoryModel.Changed += Refresh;

        BindViewEventsOnce();
        Refresh();
    }

    private void BindViewEventsOnce()
    {
        if (viewEventsBound || inventoryView == null)
        {
            return;
        }

        inventoryView.CategorySelected += ChangeCategory;
        viewEventsBound = true;
    }

    /// <summary>
    /// UIManager每次打开背包时调用。
    /// 按需求，每次打开都回到“全部”分类。
    /// </summary>
    public void Open()
    {
        currentCategory = ItemCategory.All;
        selectedDefinition = null;
        Refresh();
    }

    public void Refresh()
    {
        if (inventoryModel == null)
        {
            return;
        }

        List<ItemStack> visibleStacks = BuildVisibleStacks();

        // 如果选中的物品被消耗完，清除详情。
        if (!ContainsDefinition(
                visibleStacks,
                selectedDefinition))
        {
            selectedDefinition = null;
        }

        inventoryView.Render(
            visibleStacks,
            selectedDefinition,
            SelectItem);

        inventoryView.SetSelectedCategory(currentCategory);
        inventoryView.ShowEmptyMessage(visibleStacks.Count == 0);

        if (selectedDefinition == null)
        {
            inventoryView.ClearDetail();
        }
        else
        {
            inventoryView.ShowDetail(selectedDefinition);
        }
    }

    private void ChangeCategory(ItemCategory category)
    {
        currentCategory = category;

        // 切换分类后先不自动选择物品，等待玩家点击。
        selectedDefinition = null;

        Refresh();
    }

    private void SelectItem(ItemDefinition definition)
    {
        selectedDefinition = definition;
        Refresh();
    }

    /// <summary>
    /// 从原来的固定格子背包中生成UI要显示的列表。
    /// 不改变InventoryModel中的任何数据。
    /// </summary>
    private List<ItemStack> BuildVisibleStacks()
    {
        List<ItemStack> result = new List<ItemStack>();

        foreach (ItemStack sourceStack in inventoryModel.Slots)
        {
            // 空格子不显示。
            if (sourceStack.IsEmpty)
            {
                continue;
            }

            // 当前不是“全部”，而且物品分类不匹配时不显示。
            if (currentCategory != ItemCategory.All &&
                sourceStack.Definition.category != currentCategory)
            {
                continue;
            }

            // 查找结果列表中是否已经有同一种物品。
            ItemStack existingStack = result.Find(
                stack =>
                    stack.Definition == sourceStack.Definition);

            if (existingStack == null)
            {
                // 创建一个只供UI显示的临时ItemStack。
                result.Add(new ItemStack(
                    sourceStack.Definition,
                    sourceStack.Amount));
            }
            else
            {
                // 同一种物品在真实背包中有多个堆叠时，合并总数量。
                existingStack.Add(sourceStack.Amount);
            }
        }

        // 只排序UI临时列表，不改变InventoryModel.Slots的真实顺序。
        result.Sort(CompareItemStack);
        return result;
    }

    private int CompareItemStack(ItemStack left, ItemStack right)
    {
        int orderResult = left.Definition.sortOrder.CompareTo(
            right.Definition.sortOrder);

        if (orderResult != 0)
        {
            return orderResult;
        }

        // sortOrder相同时按名称稳定排序。
        return string.Compare(
            left.Definition.displayName,
            right.Definition.displayName,
            System.StringComparison.Ordinal);
    }

    private bool ContainsDefinition(
        List<ItemStack> stacks,
        ItemDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        foreach (ItemStack stack in stacks)
        {
            if (stack.Definition == definition)
            {
                return true;
            }
        }

        return false;
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class InventoryPresenter : MonoBehaviour
//{
//    [SerializeField]
//    private InventoryView inventoryView;//绑定之前的InventoryView 视图根节点

//    private InventoryModel inventoryModel;//在Start中获取全局唯一的数据模型

//    private void Start()
//    {
//        inventoryModel = GameBootstrap.InventoryModel;

//        if(inventoryModel==null)
//        {
//            Debug.LogError("InventoryPresenter 找不到InventoryModel，请确认GameBootstarp已启用", this);
//            return;
//        }

//        inventoryModel.Changed += Refresh;
//        Refresh();
//    }

//    private void OnDestroy()
//    {
//        if (inventoryModel != null)
//        {
//            inventoryModel.Changed -= Refresh;
//        }
//    }

//    public void Refresh()
//    {
//        inventoryView.Render(inventoryModel.Slots);
//    }


//}
