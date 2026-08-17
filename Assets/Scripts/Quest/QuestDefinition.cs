using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//ScriptableObject是纯粹的数据容器，值保存在项目的文件夹里，它不需要挂在任何游戏对象上，节省内存
[CreateAssetMenu(menuName = "Survival ARPG/Quest Definition", fileName = "Quest_")]
public class QuestDefinition : ScriptableObject
{
    public string questId;//任务的唯一ID
    public string title;//任务标题
    [TextArea] public string description;//任务的描述
    public QuestObjectiveDefinition[] objectives;//任务目标数组
    public QuestReward[] rewards;//任务奖励数组
}