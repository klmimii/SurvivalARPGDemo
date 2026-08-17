using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryView : MonoBehaviour
{
    [Header("中间物品列表")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private InventorySlotView slotPrefab;
    [SerializeField] private TMP_Text emptyText;

    [Header("左侧分类按钮")]
    [SerializeField] private Button allButton;
    [SerializeField] private Button woodButton;
    [SerializeField] private Button plantButton;
    [SerializeField] private Button mineralButton;
    [SerializeField] private Button currencyButton;
    [SerializeField] private Button monsterDropButton;
    [SerializeField] private Button recipeButton;
    [SerializeField] private Button buildingButton;
    [SerializeField] private Button potionButton;

    [Header("右侧物品详情")]
    [SerializeField] private GameObject detailContent;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text detailEmptyText;

    [Header("关闭按钮")]
    [SerializeField] private Button closeButton;

    private readonly List<InventorySlotView> activeSlots =
        new List<InventorySlotView>();

    /// <summary>
    /// 左侧分类按钮点击后通知Presenter。
    /// </summary>
    public event Action<ItemCategory> CategorySelected;

    /// <summary>
    /// 关闭按钮点击后通知UIManager。
    /// </summary>
    public event Action Closed;

    private void Awake()
    {
        allButton.onClick.AddListener(SelectAll);
        woodButton.onClick.AddListener(SelectWood);
        plantButton.onClick.AddListener(SelectPlant);
        mineralButton.onClick.AddListener(SelectMineral);
        currencyButton.onClick.AddListener(SelectCurrency);
        monsterDropButton.onClick.AddListener(SelectMonsterDrop);
        recipeButton.onClick.AddListener(SelectRecipe);
        buildingButton.onClick.AddListener(SelectBuilding);
        potionButton.onClick.AddListener(SelectPotion);
        closeButton.onClick.AddListener(RequestClose);

        ClearDetail();
    }

    private void OnDestroy()
    {
        allButton.onClick.RemoveListener(SelectAll);
        woodButton.onClick.RemoveListener(SelectWood);
        plantButton.onClick.RemoveListener(SelectPlant);
        mineralButton.onClick.RemoveListener(SelectMineral);
        currencyButton.onClick.RemoveListener(SelectCurrency);
        monsterDropButton.onClick.RemoveListener(SelectMonsterDrop);
        recipeButton.onClick.RemoveListener(SelectRecipe);
        buildingButton.onClick.RemoveListener(SelectBuilding);
        potionButton.onClick.RemoveListener(SelectPotion);
        closeButton.onClick.RemoveListener(RequestClose);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Presenter把已经过滤和合并的ItemStack列表交给View。
    /// </summary>
    public void Render(
        IReadOnlyList<ItemStack> visibleStacks,
        ItemDefinition selectedDefinition,
        Action<ItemDefinition> onItemClicked)
    {
        ClearSlots();

        foreach (ItemStack stack in visibleStacks)
        {
            InventorySlotView slot =
                Instantiate(slotPrefab, contentRoot);

            bool isSelected =
                stack.Definition == selectedDefinition;

            slot.Render(stack, isSelected, onItemClicked);
            activeSlots.Add(slot);
        }
    }

    public void ShowEmptyMessage(bool shouldShow)
    {
        emptyText.gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            emptyText.text = "当前尚未获得此种材料";
        }
    }

    public void ShowDetail(ItemDefinition definition)
    {
        if (definition == null)
        {
            ClearDetail();
            return;
        }

        detailContent.SetActive(true);
        detailEmptyText.gameObject.SetActive(false);

        detailNameText.text = definition.displayName;
        detailDescriptionText.text = definition.description;
        detailIconImage.sprite = definition.icon;
        detailIconImage.enabled = definition.icon != null;
    }

    public void ClearDetail()
    {
        if (detailContent != null)
        {
            detailContent.SetActive(false);
        }

        if (detailEmptyText != null)
        {
            detailEmptyText.gameObject.SetActive(true);
            detailEmptyText.text = "请选择一个物品";
        }
    }

    /// <summary>
    /// 当前分类按钮不可点击，用Button的Disabled颜色表示选中。
    /// </summary>
    public void SetSelectedCategory(ItemCategory category)
    {
        allButton.interactable = category != ItemCategory.All;
        woodButton.interactable = category != ItemCategory.Wood;
        plantButton.interactable = category != ItemCategory.Plant;
        mineralButton.interactable = category != ItemCategory.Mineral;
        currencyButton.interactable = category != ItemCategory.Currency;
        monsterDropButton.interactable =
            category != ItemCategory.MonsterDrop;
        recipeButton.interactable = category != ItemCategory.Recipe;
        buildingButton.interactable =
            category != ItemCategory.Building;
        potionButton.interactable = category != ItemCategory.Potion;
    }

    private void ClearSlots()
    {
        foreach (InventorySlotView slot in activeSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }

        activeSlots.Clear();
    }

    private void SelectAll()
    {
        CategorySelected?.Invoke(ItemCategory.All);
    }

    private void SelectWood()
    {
        CategorySelected?.Invoke(ItemCategory.Wood);
    }

    private void SelectPlant()
    {
        CategorySelected?.Invoke(ItemCategory.Plant);
    }

    private void SelectMineral()
    {
        CategorySelected?.Invoke(ItemCategory.Mineral);
    }

    private void SelectCurrency()
    {
        CategorySelected?.Invoke(ItemCategory.Currency);
    }

    private void SelectMonsterDrop()
    {
        CategorySelected?.Invoke(ItemCategory.MonsterDrop);
    }

    private void SelectRecipe()
    {
        CategorySelected?.Invoke(ItemCategory.Recipe);
    }

    private void SelectBuilding()
    {
        CategorySelected?.Invoke(ItemCategory.Building);
    }

    private void SelectPotion()
    {
        CategorySelected?.Invoke(ItemCategory.Potion);
    }

    private void RequestClose()
    {
        Closed?.Invoke();
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// 负责管理整个背包界面的显隐
///// 负责显示和隐藏面板/生成筛选后的物品格/接受左侧分类按钮点击，然后把 分类告诉presenter
///// 接收右上角关闭按钮点击，然后通知UIManager
///// 显示或清空右侧详情/显示当前尚未获得此种材料
///// </summary>
//public class InventoryView : MonoBehaviour
//{
//    [SerializeField]
//    private Transform contentRoot;//所有生成的格子都会自动排布在这个节点下方
//    [SerializeField]
//    private InventorySlotView slotPrefab;//格子预设体，直接引用挂有InventorySlotView脚本的Prefab

//    //激活格子列表，在内存中追踪当前界面上正在显示的每一个InventorySlotView实例，方便后续刷新UI时进行准确清理
//    private readonly List<InventorySlotView> activeSlots = new List<InventorySlotView>();

//    public void Show()
//    {
//        this.gameObject.SetActive(true);
//    }

//    public void Hide()
//    {
//        this.gameObject.SetActive(false);
//    }

//    public void Render(IReadOnlyList<ItemStack> slots)
//    {
//        //每次刷新前线清空旧格子
//        ClearSlots();

//        //遍历模型数据传进来的每个itemStack。
//        foreach(ItemStack stack in slots)
//        {
//            //通过实例化生成一个新的格子并自动作为ContentRoot的子物体
//            InventorySlotView slot = Instantiate(slotPrefab, contentRoot);
//            //调用单个各自的Render来驱动单个格子去画名字和数量
//            slot.Render(stack);
//            //将生成的格子引用加入activeSlots列表中统一管理
//            activeSlots.Add(slot);
//        }
//    }

//    private void ClearSlots()
//    {
//        //在重新生成新UI之前，把上一次生成的格子GameObject全部从场景中Destroy销毁，再清空activeSlots列表，防止残留旧数据或引发内存泄漏
//        foreach(InventorySlotView slot in activeSlots)
//        {
//            Destroy(slot.gameObject);
//        }

//        activeSlots.Clear();
//    }
//}
