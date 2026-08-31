using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 真实网络玩家的所有权入口。
/// 它不负责移动算法，只决定哪些现有脚本可以在本机读取输入，并把场景相机绑定给本机拥有的玩家。
/// 同一个Prefab在网络上会有多个实例（Host一个，每个Client各一个），但每个实例只应该影响属于自己的那个玩家操作
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayer : NetworkBehaviour//NetworkBehaviour是NGO中所有需要网络同步的脚本的基类，它能够使用NetworkVariable\调用[ServerRpc]/[ClientRpc]\是否拥有IsOwnerIsServerIsClient属性\是否有OnNetworkSpawnDespawn生命周期
{
    [Header("Required References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PartyController partyController;

    [Tooltip("只允许本机拥有者运行的输入脚本。不要把 NetworkPlayer 自己拖进来。")]
    //里面存放的是只在自己控制的角色上启用的组件列表。对于你自己的角色，这些组件enable=true，对于其他玩家角色，这些组件enable=false
    //用Behaviour而不是MonoBehaviour是因为Behaviour可以接收任何由enabled属性的组件（包括渲染器，碰撞体等）
    //这里只关联了weaponController和weaponSwitcher，是因为这两个逻辑是即时输入的，只有拥有该角色的玩家才知道自己按了什么键，如果把这个逻辑同步给所有客户端，会导致极大的带宽和不必要的计算
    //总结：将依赖玩家本机输入的、对时效性要求较高的交互逻辑归类为Owner Only Behaviours而将需要全局同步的状态（血量、动画、位置）留给NetworkPlayerState统一管理。这样设计能让网络同步更高效，代码职责也更清楚
    [SerializeField] private Behaviour[] ownerOnlyBehaviours;

    public PlayerController PlayerController => playerController;
    public PartyController PartyController => partyController;

    /// <summary>
    /// 在这个网络对象被生成时即NetworkManager.Spawn成功之后，在所有客户端上这个NetworkObject刚被生成出来的那一帧立刻执行。
    /// </summary>
    public override void OnNetworkSpawn()
    {
        //这个IsOwner时NetworkBehaviour提供的一个bool值，用来判断当前正在执行这段代码的实例，是否属于当前客户端。
        //假设你在联机游戏中Host和Client各有一个人物，那么Prefab上的脚本在两个端上各有一个实例在运行（Host自己true,Client自己true,host看到的Clientfalse，Client看到的Hostfalse）
        bool enableLocalControl = IsOwner;

        // PartyController 必须保持启用来生成远端角色外观，
        // 这里只关闭它的本机输入，而不是关闭整个组件。
        if (partyController != null)
        {
            //告诉切换角色控制器是否接收本地玩家的输入
            partyController.SetAcceptLocalInput(enableLocalControl);
        }
        if (playerController != null)
        {
            playerController.SetAcceptLocalInput(enableLocalControl);
        }

        //将ownerOnleBehaviour中的组件是否激活设为enableLocalControl，如果是IsOwner则为true，否则为false
        SetOwnerOnlyBehaviours(enableLocalControl);

        //当前脚本运行在不属于当前客户端的角色上时，打印日志并返回。也就是说如果这个角色不是我控制的，就别做本地玩家专属的初始化工作
        if (!IsOwner)
        {
            Debug.Log($"生成远端真实玩家，OwnerClientId={OwnerClientId}", this);
            return;
        }

        //只有IsOwner==true的实例才会执行
        if (GameBootstrap.InputMode != null)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
        }

        BindLocalCamera();
        LocalPlayerContext.Register(this);
        BindOwnerHud();

        // 本册使用拥有者权威位移，可以由拥有者设置初始位置。
        transform.position = new Vector3((float)OwnerClientId * 2.5f,transform.position.y,0f);

        Debug.Log($"生成本机真实玩家，ClientId={OwnerClientId}",this);
    }

    /// <summary>
    /// 当这个网络对象被销毁或取消生成时，即NetworkManager.Despawn或对象被Destroy时，在所有客户端上都会调用
    /// </summary>
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
            ThirdPersonCamera cameraController = mainCamera.GetComponent<ThirdPersonCamera>();

            if (cameraController != null)
            {
                cameraController.SetTarget(null);
            }
        }
    }

    /// <summary>
    /// 根据是否是IsOwner设置ownerOnlyBehaviours里的组件的失活激活状态
    /// </summary>
    /// <param name="value"></param>
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

    /// <summary>
    /// 绑定本地相机
    /// </summary>
    private void BindLocalCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("NetworkPlayer 找不到带 MainCamera Tag 的相机。",this);
            return;
        }

        if (playerController != null)
        {
            playerController.SetCameraTransform(mainCamera.transform);
        }

        ThirdPersonCamera cameraController = mainCamera.GetComponent<ThirdPersonCamera>();

        if (cameraController == null)
        {
            Debug.LogError("Main Camera 上没有 ThirdPersonCamera。", mainCamera);
            return;
        }

        cameraController.SetTarget(transform);

        WeaponController localWeaponController = GetComponent<WeaponController>();

        if (localWeaponController != null)
        {
            localWeaponController.SetCameraReferences(mainCamera,cameraController);

            localWeaponController.SetNetworkCombat(GetComponent<NetworkPlayerCombat>());
        }
        // 强制重新执行 OnEnable：锁定并隐藏鼠标，
        // 同时确保刚刚创建的本机玩家成为当前目标。
        cameraController.enabled = false;
        cameraController.enabled = true;
    }

    /// <summary>
    /// 绑定自己的Hud面板
    /// </summary>
    private void BindOwnerHud()
    {
        Health ownHealth = GetComponent<Health>();

        PlayerHudView healthView = Object.FindObjectOfType<PlayerHudView>(true);

        if (healthView != null)
        {
            healthView.Bind(ownHealth);
        }

        PartyPresenter partyPresenter = Object.FindObjectOfType<PartyPresenter>(true);

        if (partyPresenter != null)
        {
            partyPresenter.Bind(partyController);
        }
    }
}