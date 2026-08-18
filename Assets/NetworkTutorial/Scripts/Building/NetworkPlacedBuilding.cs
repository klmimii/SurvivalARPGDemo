using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 挂在每一种网络建筑Prefab上。
/// Definition决定它是什么；NetworkVariable保存谁建造、如何支付。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlacedBuilding))]
public sealed class NetworkPlacedBuilding : NetworkBehaviour
{
    [SerializeField]
    private BuildingDefinition definition;

    [SerializeField]
    private PlacedBuilding placedBuilding;

    private readonly NetworkVariable<ulong> builderClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> paymentSource = new(
        (int)BuildingPaymentSource.Unknown,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // 只需要服务器知道依赖关系，不必发给所有客户端。
    private NetworkPlacedBuilding serverSupport;

    public BuildingDefinition Definition => definition;
    public ulong BuilderClientId => builderClientId.Value;

    public BuildingPaymentSource PaymentSource =>
        (BuildingPaymentSource)paymentSource.Value;

    public NetworkPlacedBuilding ServerSupport => serverSupport;

    public override void OnNetworkSpawn()
    {
        paymentSource.OnValueChanged += OnPaymentSourceChanged;
        ApplyPlacedBuildingData();
    }

    public override void OnNetworkDespawn()
    {
        paymentSource.OnValueChanged -= OnPaymentSourceChanged;
    }

    /// <summary>
    /// 服务器在Spawn后调用，避免在未Spawn的NetworkBehaviour上写NetworkVariable。
    /// </summary>
    public void ServerInitialize(
        ulong ownerId,
        BuildingPaymentSource source,
        NetworkPlacedBuilding support)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        builderClientId.Value = ownerId;
        paymentSource.Value = (int)source;
        serverSupport = support;

        ApplyPlacedBuildingData();
    }

    private void OnPaymentSourceChanged(int previous, int current)
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
            playerBuilt: true,
            restoredPaymentSource: PaymentSource);
    }
}