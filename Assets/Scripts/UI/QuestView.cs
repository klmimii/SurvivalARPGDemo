using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class QuestView : MonoBehaviour
{
    [SerializeField] private Transform contentRoot;//列表容器父节点
    [SerializeField] private QuestSlotView slotPrefab;//单个格子的预设体
    [SerializeField] private Button closeButton;//关闭界面按钮


    private UIPanelTween panelTween;
    public bool IsVisible => panelTween != null
        ? panelTween.IsVisible
        : gameObject.activeInHierarchy;
    private readonly List<QuestSlotView> activeSlots = new List<QuestSlotView>();//当前正在显示的任务格子实例列表
    public event Action Closed;//界面关闭时触发的事件

    private void Awake()
    {
        panelTween = UIPanelTween.GetOrAdd(gameObject);
        closeButton.onClick.AddListener(Close);
    }

    public void Show()
    {
        UIPanelTween.GetOrAdd(gameObject).Show();
    }

    public void Close()
    {
        UIPanelTween.GetOrAdd(gameObject).Hide();
        Closed?.Invoke();
    }

    /// <summary>
    /// UI渲染与列表重建
    /// </summary>
    /// <param name="quests"></param>
    /// <param name="onClaim"></param>
    public void Render(IReadOnlyList<QuestRuntime> quests, Action<string> onClaim)
    {
        //清理阶段 销毁上一次生成的所有任务Slot游戏对象
        foreach (QuestSlotView slot in activeSlots)
        {
            Destroy(slot.gameObject);
        }

        //清理列表引用
        activeSlots.Clear();

        //根据传入的最新的quests列表，重新生成UI
        foreach (QuestRuntime quest in quests)
        {
            //实例化预制体，放置在contentRoot父节点下
            QuestSlotView slot = Instantiate(slotPrefab, contentRoot);
            //调用Slot的Bind方法填充文本数据并绑定奖励回调
            slot.Bind(quest, onClaim);
            //存入列表，方便下一次清理
            activeSlots.Add(slot);
        }
    }
}
