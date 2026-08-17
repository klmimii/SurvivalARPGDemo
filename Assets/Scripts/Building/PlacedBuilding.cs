using System;
using UnityEngine;

public class PlacedBuilding : MonoBehaviour
{
    [SerializeField] private BuildingDefinition definition;
    [SerializeField] private string instanceId;
    [SerializeField] private bool isPlayerBuilt;

    private BuildingSocket attachedSocket;

    public BuildingDefinition Definition => definition;
    public string InstanceId => instanceId;
    public bool IsPlayerBuilt => isPlayerBuilt;
    public BuildingSocket AttachedSocket => attachedSocket;

    [SerializeField]
    private BuildingPaymentSource paymentSource =BuildingPaymentSource.Unknown;

    public BuildingPaymentSource PaymentSource => paymentSource;
    /// <summary>
    /// 新建或读档生成后调用。
    /// restoredId为空时创建新的稳定实例ID。
    /// </summary>
    //public void Initialize(
    //    BuildingDefinition newDefinition,
    //    string restoredId = null,
    //    bool playerBuilt = true)
    //{
    //    definition = newDefinition;
    //    isPlayerBuilt = playerBuilt;

    //    if (!string.IsNullOrWhiteSpace(restoredId))
    //    {
    //        instanceId = restoredId;
    //    }
    //    else if (string.IsNullOrWhiteSpace(instanceId))
    //    {
    //        instanceId = Guid.NewGuid().ToString("N");
    //    }
    //}

    public void Initialize(
    BuildingDefinition newDefinition,
    string restoredId = null,
    bool playerBuilt = true,
    BuildingPaymentSource restoredPaymentSource =
        BuildingPaymentSource.Unknown)
    {
        definition = newDefinition;
        isPlayerBuilt = playerBuilt;
        paymentSource = restoredPaymentSource;

        if (!string.IsNullOrWhiteSpace(restoredId))
        {
            instanceId = restoredId;
        }
        else if (string.IsNullOrWhiteSpace(instanceId))
        {
            instanceId = System.Guid.NewGuid().ToString("N");
        }
    }

    public void AttachTo(BuildingSocket socket)
    {
        attachedSocket = socket;
    }

    public void DetachFromSocket(BuildingSocket socket)
    {
        if (attachedSocket == socket)
        {
            attachedSocket = null;
        }
    }

    private void OnDestroy()
    {
        if (attachedSocket != null)
        {
            attachedSocket.Release(this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (definition == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position,
            transform.rotation,
            Vector3.one);
        Gizmos.DrawWireCube(definition.boundsCenter, definition.boundsSize);
        Gizmos.matrix = oldMatrix;
    }
}

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class PlacedBuilding : MonoBehaviour
//{
//    [SerializeField]
//    private BuildingDefinition definition;//在Inspector面板中绑定的建筑数据配置
//    public BuildingDefinition Definition => definition;//外部只读属性，用于安全的只读访问
//}
