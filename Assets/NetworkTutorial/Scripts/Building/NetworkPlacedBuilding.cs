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

    private readonly NetworkVariable<ulong> builderClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> paymentSource = new(
        (int)BuildingPaymentSource.Unknown,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString64Bytes> instanceId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkPlacedBuilding serverSupport;

    public BuildingDefinition Definition => definition;
    public ulong BuilderClientId => builderClientId.Value;
    public string InstanceId => instanceId.Value.ToString();

    public BuildingPaymentSource PaymentSource =>
        (BuildingPaymentSource)paymentSource.Value;

    public NetworkPlacedBuilding ServerSupport => serverSupport;

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

    public void ServerInitialize(
        ulong ownerId,
        BuildingPaymentSource source,
        NetworkPlacedBuilding support,
        string restoredInstanceId = null)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        builderClientId.Value = ownerId;
        paymentSource.Value = (int)source;
        instanceId.Value = new FixedString64Bytes(
            string.IsNullOrWhiteSpace(restoredInstanceId)
                ? Guid.NewGuid().ToString("N")
                : restoredInstanceId);
        serverSupport = support;
        ApplyPlacedBuildingData();
    }

    public void ServerSetSupport(NetworkPlacedBuilding support)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer)
        {
            serverSupport = support;
        }
    }

    private void OnPaymentSourceChanged(int previous, int current)
    {
        ApplyPlacedBuildingData();
    }

    private void OnInstanceIdChanged(
        FixedString64Bytes previous,
        FixedString64Bytes current)
    {
        ApplyPlacedBuildingData();
    }

    private void ApplyPlacedBuildingData()
    {
        if (placedBuilding == null || definition == null)
        {
            return;
        }

        placedBuilding.Initialize(
            definition,
            InstanceId,
            true,
            PaymentSource);
    }
}