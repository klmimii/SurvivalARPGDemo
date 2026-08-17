using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 公共枚举类型，对物品进行分类
/// </summary>
public enum ItemType
{
    /// <summary>
    /// 材料，比如木头矿石
    /// </summary>
    Material,
    /// <summary>
    /// 消耗品，比如药水，食物
    /// </summary>
    Consumale,
    /// <summary>
    /// 建筑包/建造套件，比如地基、墙壁
    /// </summary>
    BuildingKit
}
