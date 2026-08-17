using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildPlacementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private ToastView toastView;
    [SerializeField] private BuildMenuView menuView;
    [SerializeField] private BuildingDefinition defaultDefinition;

    [Header("Input")]
    [SerializeField] private InputActionReference toggleBuildModeAction;
    [SerializeField] private InputActionReference placeBuildingAction;
    [SerializeField] private InputActionReference rotateBuildingAction;
    [SerializeField] private InputActionReference toggleDemolishAction;
    [SerializeField] private InputActionReference cancelBuildAction;

    [Header("Raycast And Collision")]
    [SerializeField] private LayerMask placementRayLayers;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LayerMask buildingLayers;
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField] private float maxPlaceDistance = 8f;

    private BuildingDefinition currentDefinition;
    private BuildingPreview currentPreview;
    private BuildingSocket currentSocket;
    private PlacedBuilding currentSupport;
    private PlacedBuilding demolishTarget;
    private BuildingPreview demolishPreview;

    private Vector3 candidatePosition;
    private Quaternion candidateRotation = Quaternion.identity;
    private float currentYaw;
    private bool canPlace;
    private bool demolishMode;

    private bool IsBuilding =>
        GameBootstrap.InputMode != null &&
        GameBootstrap.InputMode.IsBuildMode();

    private bool cachedCanAfford;
    private InventoryModel subscribedInventory;

    private void Start()
    {
        subscribedInventory = GameBootstrap.InventoryModel;

        if (subscribedInventory != null)
        {
            subscribedInventory.Changed += RefreshAffordability;
        }

        RefreshAffordability();
    }

    private void OnDestroy()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.Changed -= RefreshAffordability;
        }
    }

    private void RefreshAffordability()
    {
        cachedCanAfford = currentDefinition != null &&
            GameBootstrap.BuildingService != null &&
            GameBootstrap.BuildingService.CanAfford(currentDefinition);
    }

    private void OnEnable()
    {
        EnableAction(toggleBuildModeAction, OnToggleBuildMode);
        EnableAction(placeBuildingAction, OnPlaceOrDemolish);
        EnableAction(rotateBuildingAction, OnRotate);
        EnableAction(toggleDemolishAction, OnToggleDemolish);
        EnableAction(cancelBuildAction, OnCancel);

        menuView.DefinitionSelected += SelectDefinition;
        menuView.DemolishSelected += ToggleDemolishMode;
        menuView.CloseSelected += ExitBuildMode;
        menuView.SetVisible(false);
    }

    private void OnDisable()
    {
        DisableAction(toggleBuildModeAction, OnToggleBuildMode);
        DisableAction(placeBuildingAction, OnPlaceOrDemolish);
        DisableAction(rotateBuildingAction, OnRotate);
        DisableAction(toggleDemolishAction, OnToggleDemolish);
        DisableAction(cancelBuildAction, OnCancel);

        if (menuView != null)
        {
            menuView.DefinitionSelected -= SelectDefinition;
            menuView.DemolishSelected -= ToggleDemolishMode;
            menuView.CloseSelected -= ExitBuildMode;
        }

        ExitBuildMode();
    }

    private void Update()
    {
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
        if (GameBootstrap.InputMode == null)
        {
            return;
        }

        // 背包、制作、NPC等UI打开时，不允许F键抢占页面状态。
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
        if (!IsBuilding || IsPointerOverUI())
        {
            return;
        }

        if (demolishMode)
        {
            TryDemolishTarget();
        }
        else
        {
            TryPlaceCurrentBuilding();
        }
    }

    private void OnRotate(InputAction.CallbackContext context)
    {
        if (!IsBuilding || demolishMode)
        {
            return;
        }

        currentYaw = Mathf.Repeat(currentYaw + 90f, 360f);
    }

    private void OnToggleDemolish(InputAction.CallbackContext context)
    {
        if (IsBuilding)
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
        if (GameBootstrap.InputMode == null ||
            GameBootstrap.InputMode.CurrentMode != GameInputMode.Gameplay)
        {
            return;
        }

        GameBootstrap.InputMode.SetMode(GameInputMode.Build);
        menuView.SetVisible(true);
        demolishMode = false;
        SelectDefinition(defaultDefinition);
        toastView.Show("建造模式：左键放置，R旋转，X拆除，Esc退出。");
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

        if (menuView != null)
        {
            menuView.SetVisible(false);
        }

        if (GameBootstrap.InputMode != null &&
            GameBootstrap.InputMode.CurrentMode == GameInputMode.Build)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
        }
    }

    public void SelectDefinition(BuildingDefinition definition)
    {
        if (!IsBuilding || definition == null ||
            definition.previewPrefab == null ||
            definition.buildingPrefab == null)
        {
            return;
        }

        demolishMode = false;
        ClearDemolishPreview();
        DestroyCurrentPreview();

        currentDefinition = definition;
        RefreshAffordability();
        currentYaw = 0f;

        GameObject previewObject = Instantiate(definition.previewPrefab);
        currentPreview = previewObject.GetComponent<BuildingPreview>();

        if (currentPreview == null)
        {
            Debug.LogError(
                $"{definition.previewPrefab.name}缺少BuildingPreview。",
                definition.previewPrefab);
            Destroy(previewObject);
            currentDefinition = null;
            return;
        }

        currentPreview.SetValid(false);
        toastView.Show($"已选择：{definition.displayName}");
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

        toastView.Show(demolishMode
            ? "拆除模式：指向玩家建筑并点击左键。"
            : "已返回建造模式。");
    }

    private void UpdatePlacementPreview()
    {
        if (currentDefinition == null || currentPreview == null)
        {
            return;
        }

        Ray ray = gameplayCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue());

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxPlaceDistance,
                placementRayLayers,
                QueryTriggerInteraction.Ignore))
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
            currentSocket = BuildingSocket.FindBest(
                currentDefinition.pieceType,
                hit.point,
                currentDefinition.snapSearchRadius);
        }

        bool validSurface;

        if (currentSocket != null)
        {
            candidatePosition = currentSocket.transform.position;
            candidateRotation = currentSocket.transform.rotation *
                Quaternion.Euler(0f, currentYaw, 0f);
            currentSupport = currentSocket.Owner;
            validSurface = true;
        }
        else
        {
            candidatePosition = SnapPositionToGrid(
                hit.point,
                currentDefinition.gridSize);
            candidateRotation = Quaternion.Euler(0f, currentYaw, 0f);
            currentSupport = hit.collider.GetComponentInParent<PlacedBuilding>();

            BuildSurfaceType surface = GetSurfaceType(hit);
            validSurface = currentDefinition.AllowsSurface(surface);

            if (currentDefinition.requireSnap)
            {
                validSurface = false;
            }
        }

        // 床和墙可以把地板作为支撑面忽略。
        // 地板拼接时不忽略相邻地板，避免错误Socket造成整块重叠。
        PlacedBuilding ignoredSupport =
            currentDefinition.pieceType == BuildingPieceType.Floor
                ? null
                : currentSupport;

        bool areaFree = BuildingPlacementValidator.IsAreaFree(
            currentDefinition,
            candidatePosition,
            candidateRotation,
            blockingLayers,
            ignoredSupport);

        //bool affordable = GameBootstrap.BuildingService != null &&
        //    GameBootstrap.BuildingService.CanAfford(currentDefinition);
        bool affordable = cachedCanAfford;

        canPlace = validSurface && areaFree && affordable;
        currentPreview.SetPose(candidatePosition, candidateRotation);
        currentPreview.SetValid(canPlace);
    }

    private void TryPlaceCurrentBuilding()
    {
        if (currentDefinition == null || !canPlace)
        {
            toastView.Show("当前位置无效、被阻挡或材料不足。");
            return;
        }

        // 点击发生后再校验一次，避免预览更新与点击之间状态变化。
        if (!BuildingPlacementValidator.IsAreaFree(
                currentDefinition,
                candidatePosition,
                candidateRotation,
                blockingLayers,
                currentDefinition.pieceType == BuildingPieceType.Floor
                    ? null
                    : currentSupport))
        {
            toastView.Show("位置刚刚被其他物体占用。");
            return;
        }

        //InventoryOperationResult consumeResult =
        //    GameBootstrap.BuildingService
        //        .TryConsumeBuildingCost(currentDefinition);

        BuildingConsumeResult consumeResult =
    GameBootstrap.BuildingService
        .TryConsumeBuildingCost(currentDefinition);

        if (!consumeResult.Success)
        {
            toastView.Show(consumeResult.Message);
            return;
        }

        GameObject instance = Instantiate(
            currentDefinition.buildingPrefab,
            candidatePosition,
            candidateRotation);

        PlacedBuilding placedBuilding =
            instance.GetComponent<PlacedBuilding>();

        if (placedBuilding == null)
        {
            Destroy(instance);
            //GameBootstrap.BuildingService
            //    .TryRefundBuildingCost(currentDefinition);

            GameBootstrap.BuildingService.TryRefundBuildingCost(currentDefinition,consumeResult.PaymentSource,true);
            toastView.Show("正式Prefab缺少PlacedBuilding，材料已回滚。");
            return;
        }

        //placedBuilding.Initialize(currentDefinition);
        placedBuilding.Initialize(currentDefinition,restoredPaymentSource: consumeResult.PaymentSource);

        if (currentSocket != null &&
            !currentSocket.TryReserve(placedBuilding))
        {
            Destroy(instance);
            //GameBootstrap.BuildingService
            //    .TryRefundBuildingCost(currentDefinition);

            GameBootstrap.BuildingService.TryRefundBuildingCost(currentDefinition,consumeResult.PaymentSource,true);
            toastView.Show("吸附点已被占用，材料已回滚。");
            return;
        }

        if (GameBootstrap.Events != null)
        {
            GameBootstrap.Events.PublishBuildingPlaced(currentDefinition);
        }
        else
        {
            Debug.LogError("GameBootstrap.Events为空，任务无法收到建造事件。");
        }


        //toastView.Show($"已建造：{currentDefinition.displayName}");
        toastView.Show($"已建造：{currentDefinition.displayName}\n" +consumeResult.Message);

        // 不退出Build模式，允许继续建造同类物体。
        canPlace = false;
    }

    private void UpdateDemolishTarget()
    {
        Ray ray = gameplayCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue());

        PlacedBuilding newTarget = null;

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxPlaceDistance,
                buildingLayers,
                QueryTriggerInteraction.Ignore))
        {
            PlacedBuilding candidate =
                hit.collider.GetComponentInParent<PlacedBuilding>();

            if (candidate != null && candidate.IsPlayerBuilt)
            {
                newTarget = candidate;
            }
        }

        if (newTarget == demolishTarget)
        {
            return;
        }

        demolishTarget = newTarget;
        ClearDemolishPreview();

        if (demolishTarget == null ||
            demolishTarget.Definition == null ||
            demolishTarget.Definition.previewPrefab == null)
        {
            return;
        }

        GameObject previewObject = Instantiate(
            demolishTarget.Definition.previewPrefab,
            demolishTarget.transform.position,
            demolishTarget.transform.rotation);

        demolishPreview = previewObject.GetComponent<BuildingPreview>();
        demolishPreview?.SetValid(false);
    }

    private void TryDemolishTarget()
    {
        if (demolishTarget == null ||
            demolishTarget.Definition == null ||
            !demolishTarget.IsPlayerBuilt)
        {
            toastView.Show("没有选中可拆除的玩家建筑。");
            return;
        }

        BuildingDefinition definition = demolishTarget.Definition;
        //InventoryOperationResult refundResult =
        //    GameBootstrap.BuildingService
        //        .TryRefundBuildingCost(definition);

        InventoryOperationResult refundResult =
    GameBootstrap.BuildingService.TryRefundBuildingCost(
        definition,
        demolishTarget.PaymentSource);

        if (!refundResult.Success)
        {
            toastView.Show(refundResult.Message);
            return;
        }

        PlacedBuilding buildingToDestroy = demolishTarget;
        demolishTarget = null;
        ClearDemolishPreview();
        Destroy(buildingToDestroy.gameObject);
        toastView.Show($"已拆除{definition.displayName}，材料已返还。");
    }

    private BuildSurfaceType GetSurfaceType(RaycastHit hit)
    {
        PlacedBuilding building =
            hit.collider.GetComponentInParent<PlacedBuilding>();

        if (building != null &&
            building.Definition != null &&
            building.Definition.pieceType == BuildingPieceType.Floor)
        {
            return BuildSurfaceType.Floor;
        }

        if (LayerIsInMask(hit.collider.gameObject.layer, groundLayers))
        {
            return BuildSurfaceType.Ground;
        }

        return BuildSurfaceType.None;
    }

    private static Vector3 SnapPositionToGrid(
        Vector3 position,
        float gridSize)
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

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
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

    private static void EnableAction(
        InputActionReference reference,
        System.Action<InputAction.CallbackContext> callback)
    {
        if (reference == null)
        {
            return;
        }

        reference.action.Enable();
        reference.action.performed += callback;
    }

    private static void DisableAction(
        InputActionReference reference,
        System.Action<InputAction.CallbackContext> callback)
    {
        if (reference == null)
        {
            return;
        }

        reference.action.performed -= callback;
        reference.action.Disable();
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.InputSystem;

//public class BuildPlacementController : MonoBehaviour
//{
//    [Header("References")]
//    [SerializeField]
//    private Camera gameplayCamera;//得到摄像机，用于从摄像机向鼠标指针位置发射物理射线
//    [SerializeField]
//    private WeaponController weaponController;//用于在进入建造模式时金庸玩家攻击，避免安左键放置建筑时挥拳误伤
//    [SerializeField]
//    private ToastView toastView;//用于弹出进入建造模式、缺少篝火套件等UI提示文字
//    [SerializeField]
//    private BuildingDefinition campfireDefinition;//默认的建筑数据配置，比如按下f键默认建造篝火

//    [Header("Input")]
//    [SerializeField]
//    private InputActionReference toggleBuildModeAction;
//    [SerializeField]
//    private InputActionReference placeBuildingAciton;
//    [SerializeField]
//    private InputActionReference rotateBuildingAction;
//    [SerializeField]
//    private InputActionReference cancelBuildAction;

//    [Header("Placement")]
//    [SerializeField]
//    private LayerMask groundLayers;//地面层级遮罩，射线只检测地面，忽略地形树木等
//    [SerializeField]
//    private LayerMask blockingLayers;//阻挡层级遮罩，比如其他建筑，石头，NPC用于检测该位置是否被占用 
//    [SerializeField]
//    private float maxPlaceDistance = 6f;//最远建造距离，比如六米内才能建造
//    [SerializeField]
//    private float overlapRadius = 0.7f;//放置点的重叠检测球体半径，用来判断有没有和别的建筑挤在一起
//    [SerializeField]
//    private float rotateStep = 45f;//每次按右键旋转的角度

//    private BuildingDefinition currentDefinition;
//    private GameObject previewObject;
//    private float currentYaw;
//    private bool canPlace;

//    private void OnEnable()
//    {
//        toggleBuildModeAction.action.Enable();
//        placeBuildingAciton.action.Enable();
//        rotateBuildingAction.action.Enable();
//        cancelBuildAction.action.Enable();

//        toggleBuildModeAction.action.performed += OnToggleBuildMode;
//        placeBuildingAciton.action.performed += OnPlaceBuilding;
//        rotateBuildingAction.action.performed += OnRotateBuilding;
//        cancelBuildAction.action.performed += OnCancelBuild;

//    }

//    private void OnDisable()
//    {

//        toggleBuildModeAction.action.performed -= OnToggleBuildMode;
//        placeBuildingAciton.action.performed -= OnPlaceBuilding;
//        rotateBuildingAction.action.performed -= OnRotateBuilding;
//        cancelBuildAction.action.performed -= OnCancelBuild;

//        toggleBuildModeAction.action.Disable();
//        placeBuildingAciton.action.Disable();
//        rotateBuildingAction.action.Disable();
//        cancelBuildAction.action.Disable();

//        ExitBuildMode();
//    }

//    private void Update()
//    {
//        //如果当前建筑定义不为空，则更新视图,此时不为空，说明当前处于建造模式中
//        if (currentDefinition != null)
//        {
//            //每帧更新预览物体的位置，旋转和红绿显示颜色
//            UpdatePreview();
//        }
//    }

//    /// <summary>
//    /// 按F键切换建造模式
//    /// </summary>
//    /// <param name="context"></param>
//    private void OnToggleBuildMode(InputAction.CallbackContext context)
//    {
//        //如果当前不在建造模式
//        if(currentDefinition==null)
//        {
//            //则按下F键进入建造模式，默认选择篝火
//            EnterBuildMode(campfireDefinition);

//        }
//        else
//        {
//            //如果当前已在建造模式中，再按一次F键直接退出建造模式
//            ExitBuildMode();
//        }
//    }

//    /// <summary>
//    /// 处理玩家点击鼠标左键确认放置建筑时的最终校验、资源扣除、实体生成和状态重置
//    /// </summary>
//    /// <param name="context"></param>
//    private void OnPlaceBuilding(InputAction.CallbackContext context)
//    {

//        //如果不在建筑模式，直接返回，当前建造模式有明确行为，镜头不转鼠标显示。如果希望建造时旋转镜头， 应使用独立的BuildCameraInput，而不是让GameplayLook和Build光标同时生效
//        if(GameBootstrap.InputMode==null||!GameBootstrap.InputMode.IsBuildMode())
//        {
//            return;
//        }    

//        //如果当前不处于建造模式或者算出来的放置不合法，直接return拦截
//        if (currentDefinition == null||!canPlace)
//        {
//            return;
//        }

//        //调用服务层，通过GameBootStrap尝试去调用背包系统里扣除构造该建筑所需的套件，比如1个Item_CampfireKit
//        InventoryOperationResult result = GameBootstrap.BuildingService.TryConsumeBuildingKit(currentDefinition);

//        //如果返回的结果不成功系统就会飘字提示：缺少篝火套件并终止后续逻辑
//        if(!result.Success)
//        {
//            toastView.Show("缺少篝火套件。");
//            return;
//        }

//        //在扣除背包物品成功后，游戏 会在半透明预览模型当前所在位置和旋转角度实例化生成真正的、带碰撞体和完整逻辑的建筑预制体
//        GameObject buildingObject = Instantiate(currentDefinition.buildingPrefab,previewObject.transform.position,previewObject.transform.rotation);

//        //将现在建造的建筑推送给建筑放置事件
//        GameBootstrap.Events.PublishBuildingPlaced(currentDefinition);

//        //UI飘字反馈已建造
//        toastView.Show($"已建造：{currentDefinition.displayName}");
//        //建造完成后，主动调用ExitBuildMode清空当前建筑配置，并重新恢复玩家的攻击功能
//        ExitBuildMode();
//    }

//    /// <summary>
//    /// 旋转预览模型,玩家按下右键时触发
//    /// </summary>
//    /// <param name="context"></param>
//    private void OnRotateBuilding(InputAction.CallbackContext context)
//    {
//        //先判断currentDefinition!=null，确保玩家确实处于建造模式中
//        if(currentDefinition!=null)
//        {
//            //旋转角度累加，将偏航角currentYaw加上步长rotateStep
//            currentYaw += rotateStep;
//        }
//    }

//    /// <summary>
//    /// 玩家按下ESC键，时取消建造
//    /// </summary>
//    /// <param name="context"></param>
//    private void OnCancelBuild(InputAction.CallbackContext context)
//    {
//        ExitBuildMode();
//    }

//    /// <summary>
//    /// 进入建造模式
//    /// </summary>
//    /// <param name="definition"></param>
//    public void EnterBuildMode(BuildingDefinition definition)
//    {
//        //如果默认定义为空或者预览预设体为空或者建造预设体为空 直接提示并返回
//        if (definition == null || definition.previewPrefab == null || definition.buildingPrefab == null)
//        {
//            toastView.Show("建筑配置不完整。");
//            return;
//        }

//        currentDefinition = definition;//记录当前建筑的建筑数据
//        currentYaw = 0f;//重置旋转角度为0
//        previewObject = Instantiate(definition.previewPrefab);//实例化半透明预览物体

//        GameBootstrap.InputMode.SetMode(GameInputMode.Build);

//        weaponController.enabled = false;//禁用攻击功能（防止左键放置时挥拳）
//        toastView.Show("建造模式：左键放置，右键旋转，ESC取消");//弹出UI操作指引
//    }

//    /// <summary>
//    /// 退出建造模式
//    /// </summary>
//    public void ExitBuildMode()
//    {
//        //销毁 场景中的 半透明预览模型
//        if(previewObject!=null)
//        {
//            Destroy(previewObject);
//        }
//        //状态重置为null，currentDefinition为空后，就会停止检测
//        previewObject = null;
//        currentDefinition = null;

//        //恢复玩家攻击能力
//        if (weaponController != null)
//        {
//            weaponController.enabled = true;
//        }

//        if (GameBootstrap.InputMode!=null&&GameBootstrap.InputMode.IsBuildMode())
//        {
//            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
//        }
//    }

//    private void UpdatePreview()
//    {
//        //1.发起物理射线，从摄像机穿过鼠标在屏幕上的坐标位置，向世界发出一条射线
//        Ray ray = gameplayCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

//        //2.地面检测，如果在maxPlaceDistance（比如6米）范围内没打中groundLayers地面
//        if(!Physics.Raycast(ray,out RaycastHit hit,maxPlaceDistance,groundLayers))
//        {
//            previewObject.SetActive(false);//隐藏半透明模型，不显示在空中 
//            canPlace = false;//标记当前不能建造
//            return;
//        }

//        //3.打中地面，显示模型，并将其同步移动到射线的碰撞点 hit.point设置角度
//        previewObject.SetActive(true);
//        previewObject.transform.SetPositionAndRotation(hit.point, Quaternion.Euler(0f, currentYaw, 0f));

//        //4.重叠碰撞检测，以落点hit.point为圆心，overlapRadius为半径画一个求，检测有没有blockingLayers障碍物
//        canPlace = !Physics.CheckSphere(hit.point, overlapRadius, blockingLayers);

//        //5.变色提示：能盖变绿，不能变红
//        SetPreviewColor(canPlace ? Color.green : Color.red);
//    }

//    private void SetPreviewColor(Color color)
//    {
//        //遍历预览物体本身及其所有子节点（包括LOD等模型}上的Renderer组件
//        foreach(Renderer targetRenderer in previewObject.GetComponentsInChildren<Renderer>())
//        {
//            //将颜色修改为传入的颜色
//            targetRenderer.material.color = color;
//        }
//    }

//}
