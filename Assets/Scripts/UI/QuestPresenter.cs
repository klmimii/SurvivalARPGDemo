using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务列表中介控制着
/// </summary>
public class QuestPresenter : MonoBehaviour
{
    [SerializeField] private QuestView questView;//任务主界面视图
    [SerializeField] private ToastView toastView;//飘字/气泡提示控件

    private void Start()
    {
        //订阅任务数据变更事件
        GameBootstrap.QuestService.Changed += RefreshIfVisible;
    }

    private void OnDestroy()
    {
        //在脚本销毁时注销订阅，放置内存泄露
        if (GameBootstrap.QuestService != null)
        {
            GameBootstrap.QuestService.Changed -= RefreshIfVisible;
        }
    }

    public void Open()
    {
        questView.Show();//显示UI弹窗
        Refresh();//刷新界面数据
    }

    public void Refresh()
    {
        //将最新的任务列表和颁奖回调函数传给View进行渲染
        questView.Render(GameBootstrap.QuestService.Quests, ClaimReward);
    }

    /// <summary>
    /// 交互回调与飘字反馈
    /// </summary>
    /// <param name="questId"></param>
    private void ClaimReward(string questId)
    {
        //调用业务服务尝试颁奖
        InventoryOperationResult result = GameBootstrap.QuestService.TryClaimReward(questId);
        //飘字显示任务量里夷陵区或背包空间不足
        toastView.Show(result.Message);
        //颁奖后重新刷新界面（领奖按钮变灰/消失）
        Refresh();
    }

    /// <summary>
    /// 被动性能优化刷新
    /// </summary>
    private void RefreshIfVisible()
    {
        //检查UI界面当前在场景中是否可见/打开
        if (questView.gameObject.activeInHierarchy)
        {
            Refresh();
        }
    }
}
