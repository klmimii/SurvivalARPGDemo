using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestSlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;//任务标题
    [SerializeField] private TMP_Text descriptionText;//任务描述
    [SerializeField] private TMP_Text objectivesText;//任务目标进度文本
    [SerializeField] private TMP_Text statusText;//任务状态文本
    [SerializeField] private Button claimButton;//颁奖按钮     

    /// <summary>
    /// 数据绑定函数 
    /// </summary>
    /// <param name="quest"></param>
    /// <param name="onClaim"></param>
    public void Bind(QuestRuntime quest, Action<string> onClaim)
    {
        //基础文本数据填充
        titleText.text = quest.Definition.title;
        descriptionText.text = quest.Definition.description;

        //拼接子目标列表文本
        objectivesText.text = BuildObjectiveText(quest);

        //使用C#8.0模式匹配更新状态文本
        statusText.text = quest.Status switch
        {
            QuestStatus.InProgress => "进行中",
            QuestStatus.Completed => "已完成，可领取奖励",
            QuestStatus.Rewarded => "奖励已领取",
            _ => string.Empty
        };

        //显隐控制，只有当任务状态为已完成时，才显示领奖按钮
        claimButton.gameObject.SetActive(quest.Status == QuestStatus.Completed);

        //在重新事件绑定，重置并重新绑定领奖按钮的点击回调
        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(() => onClaim(quest.Definition.questId));
    }

    /// <summary>
    /// 拼接文本函数
    /// </summary>
    /// <param name="quest"></param>
    /// <returns></returns>
    private string BuildObjectiveText(QuestRuntime quest)
    {
        //使用StringBuilder避免大量字符串拼接产生的垃圾回收，string只会在原有的数组缓冲区里追加内容，不会重新创建字符串对象，零GC垃圾产生
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < quest.Definition.objectives.Length; i++)
        {
            QuestObjectiveDefinition objective = quest.Definition.objectives[i];
            //如果该子目标已完成显示勾，未完成显示×
            string prefix = quest.IsObjectiveCompleted(i) ? "■" : "□";
            //拼接成标准格式【前缀】【描述】当前进度/目标数量     
            builder.AppendLine($"{prefix} {objective.description} ({quest.Progress[i]}/{objective.requiredCount})");
        }

        return builder.ToString();
    }
}