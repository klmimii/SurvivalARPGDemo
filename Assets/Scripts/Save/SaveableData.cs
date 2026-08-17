using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 此数据结构很小，优先理解存档流程，大型项目会按照背包、角色任务、世界分别使用明确的数据DTO，而不是一个万能类
/// </summary>
[Serializable]
public class SaveableData 
{
    public string id;//物品ID
    public bool boolValue;//宝箱是否开启，任务是否完成 
    public float floatValue;//玩家当前生命值，游戏时长
    public Vector3 position;//玩家或物体在世界中的位置
    public Vector3 rotation;//旋转欧拉角 
}
