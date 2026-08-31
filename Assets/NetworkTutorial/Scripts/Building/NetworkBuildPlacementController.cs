using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 挂在 NetworkGameplayPlayer 根物体上。
/// 只有本机拥有者读取输入、打开菜单和创建本地预览；
/// 真正的扣费、生成和拆除全部交给 NetworkBuildingService 的服务器逻辑。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkBuildingService))]
public sealed class NetworkBuildPlacementController : NetworkBehaviour
{
    [Header("Player Prefab References")]
    [SerializeField]
    private NetworkBuildingService buildingService;

    [SerializeField]
    private BuildingDefinition defaultDefinition;

    [Header("Input")]
    [SerializeField]
    private InputActionReference toggleBuildModeAction;

    [SerializeField]
    private InputActionReference placeBuildingAction;

    [SerializeField]
    private InputActionReference rotateBuildingAction;

    [SerializeField]
    private InputActionReference toggleDemolishAction;

    [SerializeField]
    private InputActionReference cancelBuildAction;

    [Header("Raycast And Collision")]
    [SerializeField]
    private LayerMask placementRayLayers;

    [SerializeField]
    private LayerMask groundLayers;

    [SerializeField]
    private LayerMask buildingLayers;

    [SerializeField]
    private LayerMask blockingLayers;

    [Tooltip("只负责让摄像机射线找到鼠标所指表面，不代表允许建造的距离。")]
    [Min(10f)]
    [SerializeField]
    private float pointerRayDistance = 100f;

    [Tooltip("建筑或拆除目标距离玩家根物体的最大距离。要与服务器一致。")]
    [Min(1f)]
    [SerializeField]
    private float maxPlaceDistance = 8f;

    // 以下三个引用来自场景，网络玩家Prefab不能直接拖场景物体，
    // 所以本机玩家Spawn后用 FindObjectOfType 自动寻找。
    private Camera gameplayCamera;
    private ToastView toastView;
    private BuildMenuView menuView;

    private BuildingDefinition currentDefinition;
    private BuildingPreview currentPreview;
    private BuildingSocket currentSocket;
    private PlacedBuilding currentSupport;

    private NetworkPlacedBuilding demolishTarget;
    private BuildingPreview demolishPreview;

    private Vector3 candidatePosition;
    private Quaternion candidateRotation = Quaternion.identity;
    private float currentYaw;
    private bool canPlace;
    private bool demolishMode;
    private bool waitingForServer;
    private bool ownerInitialized;
    private bool cachedAffordable;
    private bool pointerOverUi;
    private float nextAffordabilityCheckTime;

    private bool IsBuilding =>
        GameBootstrap.InputMode != null &&
        GameBootstrap.InputMode.IsBuildMode();

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // 远端玩家只负责显示网络位置和动画，绝不能读取本机建造输入。
            enabled = false;
            return;
        }

        if (buildingService == null)
        {
            buildingService = GetComponent<NetworkBuildingService>();
        }

        ResolveSceneReferences();

        if (buildingService == null || gameplayCamera == null ||
            menuView == null)
        {
            Debug.LogError( "联网建造初始化失败：缺少 BuildingService、MainCamera 或 BuildMenuView。", this);
            enabled = false;
            return;
        }

        ownerInitialized = true;
        BindInput();
        BindMenu();
        buildingService.OwnerResultReceived += OnOwnerResultReceived;
        menuView.SetVisible(false);
    }

    public override void OnNetworkDespawn()
    {
        ShutdownOwner();
    }

    private void OnDisable()
    {
        // 离开世界、停止Play或组件被禁用时都必须解除输入订阅。
        ShutdownOwner();
    }

    private void ResolveSceneReferences()
    {
        gameplayCamera = Camera.main;
        toastView = FindObjectOfType<ToastView>(true);
        menuView = FindObjectOfType<BuildMenuView>(true);
    }

    private void BindInput()
    {
        EnableAction(toggleBuildModeAction, OnToggleBuildMode);
        EnableAction(placeBuildingAction, OnPlaceOrDemolish);
        EnableAction(rotateBuildingAction, OnRotate);
        EnableAction(toggleDemolishAction, OnToggleDemolish);
        EnableAction(cancelBuildAction, OnCancel);
    }

    private void UnbindInput()
    {
        DisableAction(toggleBuildModeAction, OnToggleBuildMode);
        DisableAction(placeBuildingAction, OnPlaceOrDemolish);
        DisableAction(rotateBuildingAction, OnRotate);
        DisableAction(toggleDemolishAction, OnToggleDemolish);
        DisableAction(cancelBuildAction, OnCancel);
    }

    private void BindMenu()
    {
        menuView.DefinitionSelected += SelectDefinition;
        menuView.DemolishSelected += ToggleDemolishMode;
        menuView.CloseSelected += ExitBuildMode;
    }

    private void UnbindMenu()
    {
        if (menuView == null)
        {
            return;
        }

        menuView.DefinitionSelected -= SelectDefinition;
        menuView.DemolishSelected -= ToggleDemolishMode;
        menuView.CloseSelected -= ExitBuildMode;
    }

    private void ShutdownOwner()
    {
        if (!ownerInitialized)
        {
            return;
        }

        ownerInitialized = false;
        UnbindInput();
        UnbindMenu();

        if (buildingService != null)
        {
            buildingService.OwnerResultReceived -= OnOwnerResultReceived;
        }

        ExitBuildMode();
    }

    private void Update()
    {
        if (!ownerInitialized)
        {
            return;
        }

        // 在普通Update阶段查询并缓存，避免在InputAction回调中查询UI。
        pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (!IsBuilding)
        {
            return;
        }

        if (demolishMode)
        {
            UpdateDemolishTarget();
        }
        else
        {
            UpdatePlacementPreview();
        }
    }

    private void OnToggleBuildMode(InputAction.CallbackContext context)
    {
        if (!ownerInitialized || GameBootstrap.InputMode == null)
        {
            return;
        }

        // 背包、制作和NPC页面打开时，不让建造键抢走UI状态。
        if (GameBootstrap.InputMode.CurrentMode == GameInputMode.UI)
        {
            return;
        }

        if (IsBuilding)
        {
            ExitBuildMode();
        }
        else
        {
            EnterBuildMode();
        }
    }

    private void OnPlaceOrDemolish(InputAction.CallbackContext context)
    {
        if (!IsBuilding || waitingForServer || pointerOverUi)
        {
            return;
        }

        if (demolishMode)
        {
            TryRequestDemolish();
        }
        else
        {
            TryRequestPlace();
        }
    }

    private void OnRotate(InputAction.CallbackContext context)
    {
        if (!IsBuilding || demolishMode || waitingForServer)
        {
            return;
        }

        currentYaw = Mathf.Repeat(currentYaw + 90f, 360f);
    }

    private void OnToggleDemolish(InputAction.CallbackContext context)
    {
        if (IsBuilding && !waitingForServer)
        {
            ToggleDemolishMode();
        }
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (IsBuilding)
        {
            ExitBuildMode();
        }
    }

    public void EnterBuildMode()
    {
        if (!ownerInitialized || GameBootstrap.InputMode == null ||
            GameBootstrap.InputMode.CurrentMode != GameInputMode.Gameplay)
        {
            return;
        }

        // Build模式会显示鼠标，并让依赖输入模式的移动、战斗和镜头停止读取输入。
        GameBootstrap.InputMode.SetMode(GameInputMode.Build);
        ApplyBuildCursor(true);

        menuView.SetVisible(true);
        demolishMode = false;
        waitingForServer = false;
        SelectDefinition(defaultDefinition);
        ShowToast("建造模式：左键放置，R旋转，X拆除，Esc退出。");
    }

    public void ExitBuildMode()
    {
        DestroyCurrentPreview();
        ClearDemolishPreview();

        currentDefinition = null;
        currentSocket = null;
        currentSupport = null;
        demolishTarget = null;
        demolishMode = false;
        canPlace = false;
        waitingForServer = false;

        if (menuView != null)
        {
            menuView.SetVisible(false);
        }

        if (GameBootstrap.InputMode != null && GameBootstrap.InputMode.CurrentMode == GameInputMode.Build)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
            ApplyBuildCursor(false);
        }
    }

    public void SelectDefinition(BuildingDefinition definition)
    {
        if (!IsBuilding || definition == null || definition.previewPrefab == null)
        {
            return;
        }

        // 联网正式Prefab由 NetworkBuildingService.entries 决定，
        // 所以这里不再检查单机的 definition.buildingPrefab。
        if (!buildingService.HasDefinition(definition))
        {
            ShowToast($"服务器建造白名单没有配置：{definition.displayName}");
            return;
        }

        demolishMode = false;
        ClearDemolishPreview();
        DestroyCurrentPreview();

        currentDefinition = definition;
        currentYaw = 0f;
        canPlace = false;
        cachedAffordable = false;
        nextAffordabilityCheckTime = 0f;

        GameObject previewObject = Instantiate(definition.previewPrefab);
        currentPreview = previewObject.GetComponent<BuildingPreview>();

        if (currentPreview == null)
        {
            Debug.LogError( $"{definition.previewPrefab.name} 缺少 BuildingPreview。",definition.previewPrefab);
            Destroy(previewObject);
            currentDefinition = null;
            return;
        }

        currentPreview.SetValid(false);
        ShowToast($"已选择：{definition.displayName}");
    }

    public void ToggleDemolishMode()
    {
        if (!IsBuilding)
        {
            return;
        }

        demolishMode = !demolishMode;
        demolishTarget = null;
        ClearDemolishPreview();

        if (currentPreview != null)
        {
            currentPreview.gameObject.SetActive(!demolishMode);
        }

        ShowToast(demolishMode ? "拆除模式：指向自己建造的建筑并点击左键。" : "已返回建造模式。");
    }

    private void UpdatePlacementPreview()
    {
        if (currentDefinition == null || currentPreview == null || gameplayCamera == null || Mouse.current == null)
        {
            return;
        }

        Ray ray = gameplayCamera.ScreenPointToRay( Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, pointerRayDistance, placementRayLayers, QueryTriggerInteraction.Ignore))
        {
            currentPreview.gameObject.SetActive(false);
            canPlace = false;
            return;
        }

        currentPreview.gameObject.SetActive(true);
        currentSocket = null;
        currentSupport = null;

        if (currentDefinition.allowSnapping)
        {
            currentSocket = BuildingSocket.FindBest( currentDefinition.pieceType, hit.point,currentDefinition.snapSearchRadius);
        }

        bool validSurface;

        if (currentSocket != null)
        {
            candidatePosition = currentSocket.transform.position;
            candidateRotation = currentSocket.transform.rotation * Quaternion.Euler(0f, currentYaw, 0f);
            currentSupport = currentSocket.Owner;
            validSurface = true;
        }
        else
        {
            candidatePosition = SnapPositionToGrid( hit.point, currentDefinition.gridSize);
            candidateRotation = Quaternion.Euler(0f, currentYaw, 0f);
            currentSupport = hit.collider.GetComponentInParent<PlacedBuilding>();

            BuildSurfaceType surface = GetSurfaceType(hit);
            validSurface = currentDefinition.AllowsSurface(surface);

            if (currentDefinition.requireSnap)
            {
                validSurface = false;
            }
        }

        PlacedBuilding ignoredSupport = currentDefinition.pieceType == BuildingPieceType.Floor ? null : currentSupport;

        bool areaFree = BuildingPlacementValidator.IsAreaFree( currentDefinition, candidatePosition, candidateRotation,blockingLayers, ignoredSupport);

        // 同时按照玩家位置限制距离，规则与服务器一致。
        bool withinPlayerDistance =
            (candidatePosition - transform.position).sqrMagnitude <=
            maxPlaceDistance * maxPlaceDistance;

        // 配方检查会创建临时字典，不需要每一帧执行。
        // 每0.1秒更新一次，视觉上仍然是即时反馈。
        if (Time.unscaledTime >= nextAffordabilityCheckTime)
        {
            cachedAffordable = buildingService.OwnerCanAfford(currentDefinition);
            nextAffordabilityCheckTime = Time.unscaledTime + 0.1f;
        }

        canPlace = validSurface && areaFree && withinPlayerDistance && cachedAffordable && !waitingForServer;

        currentPreview.SetPose(candidatePosition, candidateRotation);
        currentPreview.SetValid(canPlace);
    }

    private void TryRequestPlace()
    {
        if (currentDefinition == null || !canPlace)
        {
            ShowToast("当前位置无效、被阻挡、距离太远或材料不足。");
            return;
        }

        // 再做一次本地重叠检查，减少鼠标移动与点击之间的误差。
        if (!BuildingPlacementValidator.IsAreaFree(currentDefinition, candidatePosition, candidateRotation, blockingLayers, currentDefinition.pieceType == BuildingPieceType.Floor ? null : currentSupport))
        {
            ShowToast("位置刚刚被其他物体占用。");
            return;
        }

        waitingForServer = true;
        canPlace = false;
        currentPreview.SetValid(false);

        buildingService.RequestPlace(currentDefinition, candidatePosition,
     // 只发送玩家按R产生的旋转。
     // Socket方向由服务器重新查找并组合。
     Quaternion.Euler(0f, currentYaw, 0f));
    }

    private void UpdateDemolishTarget()
    {
        if (gameplayCamera == null || Mouse.current == null)
        {
            return;
        }

        Ray ray = gameplayCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        NetworkPlacedBuilding newTarget = null;

        if (Physics.Raycast( ray, out RaycastHit hit, pointerRayDistance,buildingLayers, QueryTriggerInteraction.Ignore))
        {
            NetworkPlacedBuilding candidate = hit.collider.GetComponentInParent<NetworkPlacedBuilding>();

            // 客户端只高亮自己建造的建筑；服务器还会再次验证所有权。
            bool withinPlayerDistance = candidate != null && (candidate.transform.position - transform.position).sqrMagnitude <= maxPlaceDistance * maxPlaceDistance;

            if (candidate != null && candidate.IsSpawned && candidate.BuilderClientId == OwnerClientId && withinPlayerDistance)
            {
                newTarget = candidate;
            }
        }

        if (newTarget == demolishTarget)
        {
            return;
        }

        demolishTarget = newTarget; ClearDemolishPreview();

        if (demolishTarget == null || demolishTarget.Definition == null || demolishTarget.Definition.previewPrefab == null)
        {
            return;
        }

        GameObject previewObject = Instantiate( demolishTarget.Definition.previewPrefab, demolishTarget.transform.position, demolishTarget.transform.rotation);

        demolishPreview = previewObject.GetComponent<BuildingPreview>();
        demolishPreview?.SetValid(false);
    }

    private void TryRequestDemolish()
    {
        if (demolishTarget == null || !demolishTarget.IsSpawned)
        {
            ShowToast("没有选中自己可拆除的网络建筑。");
            return;
        }

        waitingForServer = true;
        buildingService.RequestDemolish(demolishTarget);
    }

    private void OnOwnerResultReceived(string message)
    {
        // Toast 已经由 NetworkBuildingService 显示，这里只恢复操作状态。
        waitingForServer = false;
        canPlace = false;
        nextAffordabilityCheckTime = 0f;

        // 拆除成功或失败后都重新射线选择，避免继续持有已Despawn对象。
        demolishTarget = null;
        ClearDemolishPreview();
    }

    private BuildSurfaceType GetSurfaceType(RaycastHit hit)
    {
        PlacedBuilding building = hit.collider.GetComponentInParent<PlacedBuilding>();

        if (building != null && building.Definition != null && building.Definition.pieceType == BuildingPieceType.Floor)
        {
            return BuildSurfaceType.Floor;
        }

        if (LayerIsInMask(hit.collider.gameObject.layer, groundLayers))
        {
            return BuildSurfaceType.Ground;
        }

        return BuildSurfaceType.None;
    }

    private static Vector3 SnapPositionToGrid( Vector3 position, float gridSize)
    {
        if (gridSize <= 0f)
        {
            return position;
        }

        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.z = Mathf.Round(position.z / gridSize) * gridSize;
        return position;
    }

    private static bool LayerIsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }


    private static void ApplyBuildCursor(bool buildMode)
    {
        // GameInputModeService 已经负责鼠标；这里再明确应用一次，
        // 防止网络玩家或相机刚Spawn时的OnEnable覆盖鼠标状态。
        Cursor.visible = buildMode;
        Cursor.lockState = buildMode ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void ShowToast(string message)
    {
        if (toastView != null)
        {
            toastView.Show(message);
        }
        else
        {
            Debug.Log(message, this);
        }
    }

    private void DestroyCurrentPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview.gameObject);
        }

        currentPreview = null;
    }

    private void ClearDemolishPreview()
    {
        if (demolishPreview != null)
        {
            Destroy(demolishPreview.gameObject);
        }

        demolishPreview = null;
    }

    private static void EnableAction( InputActionReference reference, System.Action<InputAction.CallbackContext> callback)
    {
        if (reference == null)
        {
            return;
        }

        reference.action.Enable();
        reference.action.performed += callback;
    }

    private static void DisableAction(InputActionReference reference,System.Action<InputAction.CallbackContext> callback)
    {
        if (reference == null)
        {
            return;
        }

        reference.action.performed -= callback;
        reference.action.Disable();
    }
}