using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class NetworkWorldSaveService : MonoBehaviour
{
    [Header("Optional UI")]
    [SerializeField] private ToastView toastView;

    private bool loading;

    private string SavePath => Path.Combine(
        Application.persistentDataPath,
        "network_host_world.json");

    private void Update()
    {
        if (Keyboard.current == null || loading || !IsActiveHost())
        {
            return;
        }

        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            SaveWorld();
        }

        if (Keyboard.current.f7Key.wasPressedThisFrame)
        {
            StartCoroutine(LoadWorldRoutine());
        }
    }

    private bool IsActiveHost()
    {
        return NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsHost;
    }

    private NetworkObject FindHostPlayer()
    {
        if (!IsActiveHost())
        {
            return null;
        }

        return NetworkManager.Singleton.SpawnManager
            .GetPlayerNetworkObject(NetworkManager.ServerClientId);
    }

    private void SaveWorld()
    {
        NetworkObject hostPlayer = FindHostPlayer();
        if (hostPlayer == null)
        {
            Show("Host 玩家尚未生成，不能保存。");
            return;
        }

        NetworkHostWorldSaveData data = new NetworkHostWorldSaveData
        {
            savedUtc = System.DateTime.UtcNow.ToString("O"),
            hostPosition = hostPlayer.transform.position,
            hostRotation = hostPlayer.transform.eulerAngles
        };

        NetworkPlayerState state =
            hostPlayer.GetComponent<NetworkPlayerState>();
        NetworkPlayerInventory inventory =
            hostPlayer.GetComponent<NetworkPlayerInventory>();
        NetworkQuestService quests =
            hostPlayer.GetComponent<NetworkQuestService>();

        data.hostHealth = state != null ? state.CurrentHealth : 1;
        inventory?.ServerCaptureSlots(data.hostInventory);
        quests?.ServerCaptureQuests(data.hostQuests);

        CaptureBuildings(data);

        if (!CaptureGatherables(data))
        {
            Show("资源节点 Scene Save Id 重复或为空，已取消保存。");
            return;
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Host 世界保存完成：{SavePath}", this);
        Show("Host 世界保存完成（F7 可读取）。");
    }

    private void CaptureBuildings(NetworkHostWorldSaveData data)
    {
        foreach (NetworkPlacedBuilding building in
                 FindObjectsOfType<NetworkPlacedBuilding>())
        {
            if (!building.IsSpawned || building.Definition == null ||
                string.IsNullOrWhiteSpace(building.InstanceId))
            {
                continue;
            }

            data.buildings.Add(new NetworkBuildingSaveData
            {
                instanceId = building.InstanceId,
                buildingId = building.Definition.buildingId,
                position = building.transform.position,
                rotation = building.transform.eulerAngles,
                paymentSource = building.PaymentSource,
                supportInstanceId = building.ServerSupport != null
                    ? building.ServerSupport.InstanceId
                    : string.Empty
            });
        }
    }

    private bool CaptureGatherables(NetworkHostWorldSaveData data)
    {
        HashSet<string> usedIds = new HashSet<string>();

        foreach (NetworkGatherableNode node in
                 FindObjectsOfType<NetworkGatherableNode>(true))
        {
            if (string.IsNullOrWhiteSpace(node.SceneSaveId) ||
                !usedIds.Add(node.SceneSaveId))
            {
                Debug.LogError(
                    $"无效或重复的资源存档 ID：{node.SceneSaveId}",
                    node);
                return false;
            }

            data.gatherables.Add(new NetworkGatherableSaveData
            {
                sceneSaveId = node.SceneSaveId,
                available = node.IsAvailable,
                remainingRefreshSeconds =
                    node.ServerRemainingRefreshSeconds
            });
        }

        return true;
    }

    private IEnumerator LoadWorldRoutine()
    {
        if (!IsActiveHost())
        {
            yield break;
        }

        if (!File.Exists(SavePath))
        {
            Show("没有找到 Host 世界存档。");
            yield break;
        }

        loading = true;

        string json = File.ReadAllText(SavePath);
        NetworkHostWorldSaveData data =
            JsonUtility.FromJson<NetworkHostWorldSaveData>(json);

        if (data == null || data.saveVersion != 1)
        {
            Show("网络存档格式无效或版本不兼容。");
            loading = false;
            yield break;
        }

        NetworkObject hostPlayer = FindHostPlayer();
        if (hostPlayer == null)
        {
            Show("Host 玩家尚未生成，不能读取。");
            loading = false;
            yield break;
        }

        RestoreHostPlayer(data, hostPlayer);
        RestoreGatherables(data);

        yield return RestoreBuildingsRoutine(data, hostPlayer);

        loading = false;
        Debug.Log("Host 权威世界读取完成。", this);
        Show("Host 世界读取完成，所有客户端已同步。");
    }

    private void RestoreHostPlayer(
        NetworkHostWorldSaveData data,
        NetworkObject hostPlayer)
    {
        CharacterController controller =
            hostPlayer.GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        hostPlayer.transform.SetPositionAndRotation(
            data.hostPosition,
            Quaternion.Euler(data.hostRotation));

        if (controller != null)
        {
            controller.enabled = true;
        }

        hostPlayer.GetComponent<NetworkPlayerState>()?
            .ServerRestoreHealth(data.hostHealth);

        hostPlayer.GetComponent<NetworkPlayerInventory>()?
            .ServerRestoreSlots(data.hostInventory);

        hostPlayer.GetComponent<NetworkQuestService>()?
            .ServerRestoreQuests(data.hostQuests);
    }

    private void RestoreGatherables(NetworkHostWorldSaveData data)
    {
        Dictionary<string, NetworkGatherableSaveData> byId = new();

        foreach (NetworkGatherableSaveData saved in data.gatherables)
        {
            if (saved != null &&
                !string.IsNullOrWhiteSpace(saved.sceneSaveId))
            {
                byId[saved.sceneSaveId] = saved;
            }
        }

        foreach (NetworkGatherableNode node in
                 FindObjectsOfType<NetworkGatherableNode>(true))
        {
            if (byId.TryGetValue(
                    node.SceneSaveId,
                    out NetworkGatherableSaveData saved))
            {
                node.ServerRestoreState(
                    saved.available,
                    saved.remainingRefreshSeconds);
            }
        }
    }

    private IEnumerator RestoreBuildingsRoutine(
        NetworkHostWorldSaveData data,
        NetworkObject hostPlayer)
    {
        NetworkPlacedBuilding[] existing =
            FindObjectsOfType<NetworkPlacedBuilding>();

        foreach (NetworkPlacedBuilding building in existing)
        {
            if (building != null && building.IsSpawned)
            {
                building.NetworkObject.Despawn(true);
            }
        }

        // 等待旧 Socket 注销。
        yield return null;

        NetworkBuildingService buildingService =
            hostPlayer.GetComponent<NetworkBuildingService>();

        if (buildingService == null)
        {
            Debug.LogError("Host 玩家缺少 NetworkBuildingService。", hostPlayer);
            yield break;
        }

        Dictionary<string, NetworkPlacedBuilding> restoredById = new();

        foreach (NetworkBuildingSaveData saved in data.buildings)
        {
            if (buildingService.ServerTrySpawnRestored(
                    saved,
                    NetworkManager.ServerClientId,
                    out NetworkPlacedBuilding instance))
            {
                restoredById[saved.instanceId] = instance;
            }
        }

        // 等待新对象 OnNetworkSpawn 和 Socket 注册。
        yield return null;

        foreach (NetworkBuildingSaveData saved in data.buildings)
        {
            if (string.IsNullOrWhiteSpace(saved.supportInstanceId) ||
                !restoredById.TryGetValue(
                    saved.instanceId,
                    out NetworkPlacedBuilding child) ||
                !restoredById.TryGetValue(
                    saved.supportInstanceId,
                    out NetworkPlacedBuilding support))
            {
                continue;
            }

            child.ServerSetSupport(support);
        }

        BuildingSocket.RebuildReservations();
    }

    private void Show(string message)
    {
        if (toastView != null)
        {
            toastView.Show(message);
        }

        Debug.Log(message, this);
    }
}