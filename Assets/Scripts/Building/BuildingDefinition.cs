using UnityEngine;

[CreateAssetMenu(menuName = "Survival ARPG/Building Definition", fileName = "Building_")]
public class BuildingDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("稳定唯一ID。存档和未来网络都依赖它，发布后不要随意修改。")]
    public string buildingId;
    public string displayName;
    public Sprite icon;
    public BuildingPieceType pieceType;

    [Header("Prefabs")]
    public GameObject buildingPrefab;
    public GameObject previewPrefab;

    //[Header("Cost")]
    //[Tooltip("新建筑使用的多材料成本。")]
    //public BuildingCost[] costs;

    //[Range(0f, 1f)]
    //[Tooltip("拆除时返还比例。作品集第一版建议设为1。")]
    //public float refundRate = 1f;

    //[Tooltip("兼容旧篝火配置。若Costs为空，则消耗一个Required Kit。")]
    //public ItemDefinition requiredKit;

    [Header("Build Payment")]
    [Tooltip("优先从背包消耗的建筑成品，例如Item_WoodFloorKit。")]
    public ItemDefinition requiredKit;

    [Tooltip("没有建筑成品时，用于直接消耗原料的配方。")]
    public RecipeDefinition craftingRecipe;

    [Tooltip("是否允许在成品不足时直接消耗配方原料。")]
    public bool allowIngredientFallback = true;

    [Tooltip("旧高级建造版本的直接材料成本。完成迁移后保持为空。")]
    public BuildingCost[] costs;

    [Range(0f, 1f)]
    [Tooltip("拆除退款比例。当前Demo建议使用1。")]
    public float refundRate = 1f;

    [Header("Placement Surface")]
    public BuildSurfaceType allowedSurfaces = BuildSurfaceType.Ground;

    [Tooltip("是否允许寻找建筑吸附点。")]
    public bool allowSnapping;

    [Tooltip("勾选后必须找到兼容吸附点，否则不能放置。墙通常勾选。")]
    public bool requireSnap;

    [Min(0.05f)]
    public float snapSearchRadius = 0.8f;

    [Tooltip("自由放置时的网格尺寸。设为0表示不对齐网格。")]
    [Min(0f)]
    public float gridSize = 0.25f;

    [Header("Placement Bounds")]
    [Tooltip("相对于Prefab根节点的占位盒中心。")]
    public Vector3 boundsCenter = new Vector3(0f, 0.5f, 0f);

    [Tooltip("用于重叠检测的占位盒尺寸，不一定与Renderer完全相同。")]
    public Vector3 boundsSize = Vector3.one;

    [Tooltip("略微缩小检测盒，允许两个模块刚好接触。")]
    [Range(0f, 0.05f)]
    public float boundsSkin = 0.01f;

    public bool AllowsSurface(BuildSurfaceType surface)
    {
        return (allowedSurfaces & surface) != 0;
    }

    public bool HasConfiguredCost()
    {
        if (requiredKit != null)
        {
            return true;
        }

        if (craftingRecipe != null)
        {
            return true;
        }

        // 仅用于尚未完成迁移的旧资产。
        return costs != null && costs.Length > 0;
        //if (costs != null && costs.Length > 0)
        //{
        //    return true;
        //}

        //return requiredKit != null;
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//[CreateAssetMenu(menuName ="Survival ARPG/Building Definition",fileName ="Building_")]
//public class BuildingDefinition : ScriptableObject
//{
//    public string buildingId;//建筑的唯一ID
//    public string displayName;//显示名称
//    public ItemDefinition requiredKit;//所需的建筑工具包/物品
//    public GameObject buildingPrefab;//真实的建筑预制体
//    public GameObject previewPrefab;//预览/虚化（预制体）。当玩家按了建造按钮，但还在选位置阶段时，跟随鼠标/网络移动的那个半透明模型
//}
