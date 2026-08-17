using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可保存接口
/// </summary>
public interface ISaveable 
{
    string SaveId { get; }
    //抓取/捕获当前物体的状态
    SaveableData CaptureState();
    //恢复还原物体的状态
    void RestoreState(SaveableData data);
}
