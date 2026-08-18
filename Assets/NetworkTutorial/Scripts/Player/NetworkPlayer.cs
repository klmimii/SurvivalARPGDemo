using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 真实网络玩家的所有权入口。
/// 它不负责移动算法，只决定哪些现有脚本可以在本机读取输入，
/// 并把场景相机绑定给本机拥有的玩家。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PartyController partyController;

    [Tooltip("只允许本机拥有者运行的输入脚本。不要把 NetworkPlayer 自己拖进来。")]
    [SerializeField] private Behaviour[] ownerOnlyBehaviours;

    public PlayerController PlayerController => playerController;
    public PartyController PartyController => partyController;

    public override void OnNetworkSpawn()
    {
        bool enableLocalControl = IsOwner;

        // PartyController 必须保持启用来生成远端角色外观，
        // 这里只关闭它的本机输入，而不是关闭整个组件。
        if (partyController != null)
        {
            partyController.SetAcceptLocalInput(enableLocalControl);
        }
        if (playerController != null)
        {
            playerController.SetAcceptLocalInput(enableLocalControl);
        }

        SetOwnerOnlyBehaviours(enableLocalControl);

        if (!IsOwner)
        {
            Debug.Log(
                $"生成远端真实玩家，OwnerClientId={OwnerClientId}",
                this);
            return;
        }

        // 网络连接完成，开始控制角色。
        if (GameBootstrap.InputMode != null)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
        }

        BindLocalCamera();
        LocalPlayerContext.Register(this);
        BindOwnerHud();

        // 本册使用拥有者权威位移，可以由拥有者设置初始位置。
        // 避免 Host 和 Client 完全重叠。
        transform.position = new Vector3(
            (float)OwnerClientId * 2.5f,
            transform.position.y,
            0f);

        Debug.Log(
            $"生成本机真实玩家，ClientId={OwnerClientId}",
            this);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return;
        }

        if (GameBootstrap.InputMode != null)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.UI);
        }

        LocalPlayerContext.Unregister(this);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            ThirdPersonCamera cameraController =
                mainCamera.GetComponent<ThirdPersonCamera>();

            if (cameraController != null)
            {
                cameraController.SetTarget(null);
            }
        }
    }

    private void SetOwnerOnlyBehaviours(bool value)
    {
        if (ownerOnlyBehaviours == null)
        {
            return;
        }

        foreach (Behaviour behaviour in ownerOnlyBehaviours)
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            behaviour.enabled = value;
        }
    }

    private void BindLocalCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError(
                "NetworkPlayer 找不到带 MainCamera Tag 的相机。",
                this);
            return;
        }

        if (playerController != null)
        {
            playerController.SetCameraTransform(
                mainCamera.transform);
        }

        ThirdPersonCamera cameraController =
            mainCamera.GetComponent<ThirdPersonCamera>();

        if (cameraController == null)
        {
            Debug.LogError(
                "Main Camera 上没有 ThirdPersonCamera。",
                mainCamera);
            return;
        }

        cameraController.SetTarget(transform);

        WeaponController localWeaponController =
    GetComponent<WeaponController>();

        if (localWeaponController != null)
        {
            localWeaponController.SetCameraReferences(
                mainCamera,
                cameraController);

            localWeaponController.SetNetworkCombat(
    GetComponent<NetworkPlayerCombat>());
        }
        // 强制重新执行 OnEnable：锁定并隐藏鼠标，
        // 同时确保刚刚创建的本机玩家成为当前目标。
        cameraController.enabled = false;
        cameraController.enabled = true;
    }
    private void BindOwnerHud()
    {
        Health ownHealth = GetComponent<Health>();

        PlayerHudView healthView =
            Object.FindObjectOfType<PlayerHudView>(true);

        if (healthView != null)
        {
            healthView.Bind(ownHealth);
        }

        PartyPresenter partyPresenter =
            Object.FindObjectOfType<PartyPresenter>(true);

        if (partyPresenter != null)
        {
            partyPresenter.Bind(partyController);
        }
    }
}