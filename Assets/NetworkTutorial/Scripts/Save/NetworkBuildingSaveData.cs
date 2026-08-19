using System;
using UnityEngine;

[Serializable]
public sealed class NetworkBuildingSaveData
{
    public string instanceId;
    public string buildingId;
    public Vector3 position;
    public Vector3 rotation;
    public BuildingPaymentSource paymentSource;
    public string supportInstanceId;
}