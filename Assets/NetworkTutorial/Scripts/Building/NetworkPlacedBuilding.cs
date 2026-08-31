using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlacedBuilding))]
public sealed class NetworkPlacedBuilding : NetworkBehaviour
{
    [SerializeField] private BuildingDefinition definition;
    [SerializeField] private PlacedBuilding placedBuilding;

    //自动同步的数据：建造者ID，支付方式，实例ID
    private readonly NetworkVariable<ulong> builderClientId = new( ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> paymentSource = new( (int)BuildingPaymentSource.Unknown,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString64Bytes> instanceId = new( default,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkPlacedBuilding serverSupport;

    public BuildingDefinition Definition => definition;
    public ulong BuilderClientId => builderClientId.Value;
    public string InstanceId => instanceId.Value.ToString();

    public BuildingPaymentSource PaymentSource =>(BuildingPaymentSource)paymentSource.Value;

    public NetworkPlacedBuilding ServerSupport => serverSupport;

    //在建筑生成时，订阅NetworkVariable变化，应用数据到本地PlacedBuilding
    public override void OnNetworkSpawn()
    {
        paymentSource.OnValueChanged += OnPaymentSourceChanged;
        instanceId.OnValueChanged += OnInstanceIdChanged;
        ApplyPlacedBuildingData();
    }

    public override void OnNetworkDespawn()
    {
        paymentSource.OnValueChanged -= OnPaymentSourceChanged;
        instanceId.OnValueChanged -= OnInstanceIdChanged;
    }

    /// <summary>
    /// 网络数据初始化
    /// </summary>
    /// <param name="ownerId"></param>
    /// <param name="source"></param>
    /// <param name="support"></param>
    /// <param name="restoredInstanceId"></param>
    public void ServerInitialize(ulong ownerId,BuildingPaymentSource source,NetworkPlacedBuilding support,string restoredInstanceId = null)
    {
        if (NetworkManager.Singleton == null ||!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        builderClientId.Value = ownerId;//存网络数据
        paymentSource.Value = (int)source;//存网络数据
        instanceId.Value = new FixedString64Bytes( string.IsNullOrWhiteSpace(restoredInstanceId) ? Guid.NewGuid().ToString("N"): restoredInstanceId);//存网络数据
        serverSupport = support;
        ApplyPlacedBuildingData();//调用了ApplyPlacedBuildingData
    }

    public void ServerSetSupport(NetworkPlacedBuilding support)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            serverSupport = support;
        }
    }

    private void OnPaymentSourceChanged(int previous, int current)
    {
        ApplyPlacedBuildingData();
    }

    private void OnInstanceIdChanged(FixedString64Bytes previous,FixedString64Bytes current)
    {
        ApplyPlacedBuildingData();
    }

    /// <summary>
    /// 把网络数据应用到本地的PlacedBuilding组件（显示颜色，模型等）
    /// </summary>
    private void ApplyPlacedBuildingData()
    {
        if (placedBuilding == null || definition == null)
        {
            return;
        }

        placedBuilding.Initialize(definition,InstanceId,true,PaymentSource);
    }
}