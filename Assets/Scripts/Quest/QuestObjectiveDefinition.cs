using System;
using UnityEngine;

/// <summary>
/// 用来配置单个任务目标的数据结构，例如击杀五只哥布林或收集3个木材
/// </summary>
[Serializable]
public class QuestObjectiveDefinition
{
    public QuestObjectiveType type;//选择当前目标的类型

    [Tooltip("物品 ID、敌人 ID、配方 ID、建筑 ID 或 Boss ID。")]
    public string targetId;//目标的唯一标识符如goblin_01

    [Min(1)] public int requiredCount = 1;//需要完成的目标数量
    [TextArea] public string description;//怪目标的文本描述
}