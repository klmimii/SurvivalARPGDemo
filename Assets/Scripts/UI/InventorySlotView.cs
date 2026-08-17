using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotView : MonoBehaviour
{
    [Header("物品格组件")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;

    [Tooltip("选中物品时显示的边框；如果暂时没做，可以不绑定。")]
    [SerializeField] private GameObject selectedFrame;

    private ItemDefinition currentDefinition;
    private Action<ItemDefinition> clickedCallback;

    private void Awake()
    {
        // 如果Inspector忘记绑定Button，尝试从当前物体取得。
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    /// <summary>
    /// InventoryView创建格子后调用。
    /// stack仍然是前面教程中的ItemStack，没有创建新数据类型。
    /// </summary>
    public void Render(ItemStack stack,bool isSelected,Action<ItemDefinition> onClicked)
    {
        currentDefinition = stack.Definition;
        clickedCallback = onClicked;

        iconImage.sprite = stack.Definition.icon;
        iconImage.enabled = stack.Definition.icon != null;

        countText.text = $"x{stack.Amount}";

        if (selectedFrame != null)
        {
            selectedFrame.SetActive(isSelected);
        }
    }

    private void HandleClick()
    {
        if (currentDefinition == null)
        {
            return;
        }

        clickedCallback?.Invoke(currentDefinition);
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using TMPro;
//using UnityEngine;

//public class InventorySlotView : MonoBehaviour
//{
//    [SerializeField]
//    private TMP_Text nameText;
//    [SerializeField]
//    private TMP_Text countText;

//    //数据驱动渲染，接受一个Itemstack，从里面拿到静态配置文件里定义的物品名称和当前格子的实际数量
//    public void Render(ItemStack stack)
//    {
//        if (stack.IsEmpty)
//        {
//            nameText.text = "空";
//            countText.text = string.Empty;
//            return;
//        }

//        nameText.text = stack.Definition.displayName;
//        countText.text = $"x{stack.Amount}";
//    }
//}
