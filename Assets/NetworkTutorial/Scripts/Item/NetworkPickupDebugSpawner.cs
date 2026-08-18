using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第6册验收工具：本机拥有者按 F7，请求服务器生成一个掉落物。
/// 第7册完成怪物掉落后可禁用或删除。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPickupDebugSpawner : NetworkBehaviour
{
    [SerializeField]
    private NetworkPickupItem pickupPrefab;

    [SerializeField]
    private ItemDefinition testItem;

    [Min(1)]
    [SerializeField]
    private int amount = 1;

    [Min(0.1f)]
    [SerializeField]
    private float spawnForwardDistance = 1.5f;

    private float nextAllowedRequestTime;

    private void Update()
    {
        if (!IsSpawned || !IsOwner || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f7Key.wasPressedThisFrame)
        {
            RequestSpawnServerRpc();
        }
    }

    [ServerRpc]
    private void RequestSpawnServerRpc()
    {
        if (Time.time < nextAllowedRequestTime ||
            pickupPrefab == null || testItem == null)
        {
            return;
        }

        nextAllowedRequestTime = Time.time + 0.5f;

        Vector3 spawnPosition =
            transform.position + transform.forward * spawnForwardDistance;
        spawnPosition.y += 0.35f;

        NetworkPickupItem pickup = Instantiate(
            pickupPrefab,
            spawnPosition,
            Quaternion.identity);

        pickup.ServerInitialize(testItem, amount);
        pickup.NetworkObject.Spawn(true);
    }
}