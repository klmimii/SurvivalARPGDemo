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
    /// 把建筑的数据（定义、ID、建造者来源）存到本地组件里，供客户端显示使用
    /// </summary>
    /// <param name="newDefinition"></param>
    /// <param name="restoredId"></param>
    /// <param name="playerBuilt"></param>
    /// <param name="restoredPaymentSource"></param>
    public void Initialize(BuildingDefinition newDefinition, string restoredId = null,  bool playerBuilt = true, BuildingPaymentSource restoredPaymentSource = BuildingPaymentSource.Unknown)
    {
        definition = newDefinition;//存建筑配置
        isPlayerBuilt = playerBuilt;//存是否玩家建造
        paymentSource = restoredPaymentSource;//存支付方式

        if (!string.IsNullOrWhiteSpace(restoredId))//从存档恢复，用存档里的ID
        {
            instanceId = restoredId;
        }
        else if (string.IsNullOrWhiteSpace(instanceId))//新建，生成一个新ID
        {
            instanceId = System.Guid.NewGuid().ToString("N");//生成全球唯一的32位字符串ID，赋值给instance ID
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
