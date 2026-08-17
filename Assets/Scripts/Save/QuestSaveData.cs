using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// 任务数据保存脚本
/// </summary>
[Serializable]
public class QuestSaveData
{
    public string questId;
    public QuestStatus status;
    public int[] progress;
}