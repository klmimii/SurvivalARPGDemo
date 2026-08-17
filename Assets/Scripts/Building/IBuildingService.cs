public interface IBuildingService
{
    //bool CanAfford(BuildingDefinition definition);

    //InventoryOperationResult TryConsumeBuildingCost(BuildingDefinition definition);

    //InventoryOperationResult TryRefundBuildingCost(BuildingDefinition definition);
    bool CanAfford(BuildingDefinition definition);

    BuildingConsumeResult TryConsumeBuildingCost(
        BuildingDefinition definition);

    InventoryOperationResult TryRefundBuildingCost(
        BuildingDefinition definition,
        BuildingPaymentSource paymentSource,
        bool forceFullRefund = false);
}


//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// 建筑服务接口
///// </summary>
//public interface IBuildingService
//{
//    /// <summary>
//    /// 返回尝试扣除消耗建筑工具包后的结果状态
//    /// </summary>
//    /// <param name="definition">传入你之前定义的建筑数据资产</param>
//    /// <returns></returns>
//    InventoryOperationResult TryConsumeBuildingKit(BuildingDefinition definition);
//}
