using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第8册临时验收：F8在玩家前方建造，F10拆除准星指向的建筑。
/// 第9册正式UI完成后可以禁用或删除。
/// </summary>
public sealed class NetworkBuildingDebugInput : MonoBehaviour
{
    [SerializeField]
    private NetworkBuildingService buildingService;

    [SerializeField]
    private BuildingDefinition testDefinition;

    [Min(1f)]
    [SerializeField]
    private float forwardDistance = 3f;

    [Min(1f)]
    [SerializeField]
    private float demolishRayDistance = 10f;

    [SerializeField]
    private LayerMask buildingLayers;

    private float yaw;

    private void Update()
    {
        if (buildingService == null || !buildingService.IsOwner ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            yaw = Mathf.Repeat(yaw + 90f, 360f);
        }

        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            Vector3 position = transform.position +
                transform.forward * forwardDistance;

            buildingService.RequestPlace(
                testDefinition,
                position,
                Quaternion.Euler(0f, yaw, 0f));
        }

        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            TryRequestDemolish();
        }
    }

    private void TryRequestDemolish()
    {
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            return;
        }

        Ray ray = gameplayCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                demolishRayDistance,
                buildingLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        NetworkPlacedBuilding target =
            hit.collider.GetComponentInParent<NetworkPlacedBuilding>();

        if (target != null)
        {
            buildingService.RequestDemolish(target);
        }
    }
}
