using System;
using UnityEngine;

[Serializable]
public class BuildingSaveData
{
    public string instanceId;
    public string buildingId;
    public Vector3 position;
    public Vector3 rotation;
    public BuildingPaymentSource paymentSource;
}

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//[Serializable]
//public class BuildingSaveData 
//{
//    public string buildingId;//建筑唯一标识ID
//    public Vector3 position;//建筑放在地图上的三维坐标
//    public Vector3 rotation;//建筑放置似的旋转欧拉角
//}
