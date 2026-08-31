using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 网络同步状态管理器
/// 同步一名玩家的角色、武器、血量和基础动画状态。
/// 持续状态由服务器写 NetworkVariable；瞬时动画由服务器转发 ClientRpc。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerState : NetworkBehaviour
{
    [Header("Existing Player Components")]
    [SerializeField] private PartyController partyController;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private Health health;

    [Header("Stable Weapon Order")]
    [Tooltip("固定顺序：0匕首、1弓、2长刀。Host和Client必须一致。")]
    [SerializeField] private WeaponDefinition[] weapons;

    [Header("Network Rate")]
    [Min(0.05f)]
    [SerializeField] private float locomotionSendInterval = 0.1f;//运动发送间隔

    //持续状态血量、武器索引、角色索引、移动速度、是否在地面由NetworkVariable同步
    //NetworkVariable时NGO提供的自动同步的网络存储变量，它的核心特点是只有你指定的权限方可以修改他的value，所有客户端都能读到value,一旦他的Value发生变化，NGO自动把新值广播给所有客户端，客户端的OnValueChanged回调会被触发
    private readonly NetworkVariable<int> characterIndex = new(
        0,//初始值，对象刚生成时这个NetworkVariable的默认值
        NetworkVariableReadPermission.Everyone,//谁可以读，所有客户端和服务器都能读取
        NetworkVariableWritePermission.Server);//谁可以写。只有服务器可以修改value。客户端修改会报错或无效

    private readonly NetworkVariable<int> weaponIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);//服务器权威架构的核心原则，关键状态只能由服务器修改，客户端只能通过ServerRpc提出修改请求

    private readonly NetworkVariable<int> currentHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> maxHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> moveSpeed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> grounded = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    //正在应用网络状态时，暂时屏蔽本地事件响应，避免本地逻辑把刚同步过来的状态又反向发回网络，造成无限循环
    private bool applyingNetworkState;
    private float nextLocomotionSendTime;

    public int CurrentHealth => currentHealth.Value;
    public int MaxHealth => maxHealth.Value;
    public bool IsDead => currentHealth.Value <= 0;

    public int CurrentWeaponIndex => weaponIndex.Value;

    /// <summary>
    /// 网络对象生成时，订阅所有事件、设置动画数据来源，如果是服务器就初始化状态，手动应用一次当前值
    /// </summary>
    public override void OnNetworkSpawn()
    {
        characterIndex.OnValueChanged += OnCharacterIndexChanged;
        weaponIndex.OnValueChanged += OnWeaponIndexChanged;
        currentHealth.OnValueChanged += OnCurrentHealthChanged;
        maxHealth.OnValueChanged += OnMaxHealthChanged;
        moveSpeed.OnValueChanged += OnMoveSpeedChanged;
        grounded.OnValueChanged += OnGroundedChanged;

        partyController.ActiveMemberChanged += OnLocalCharacterChanged;
        weaponController.WeaponEquipped += OnLocalWeaponEquipped;

        //跳跃、闪避、攻击事件只有本机玩家才需要输入，所以要判断是不是本机玩家
        if (IsOwner)
        {
            playerController.JumpStarted += OnLocalJump;
            playerController.DodgeStarted += OnLocalDodge;
            weaponController.AttackStarted += OnLocalAttack;
        }

        //本机玩家不使用网络同步的移动动画，远端玩家使用网络同步的移动动画。
        animationController.SetUseNetworkLocomotion(!IsOwner);

        //所有NetworkVariable的写入权限都是NetworkVariableWritePermission.Server，所以只有IsServer==true的实例能够执行xxx.value=yyy
        //只有Host上的A玩家能够执行，
        if (IsServer)
        {
            //给所有NetworkVariable设置初始值
            //客户端的NetworkVariable怎么有值？服务器执行InitializeServerState，设置初始值，然后NGO引擎自动把这些新值打包成网络消息，广播给所有客户端，客户端收到消息，自动更新本地的NetVariable缓存
            //客户端的CharacterIndex.value现在是0，但客户端没有执行过赋值代码
            InitializeServerState();
        }

        //NetworkVariable有一个OnValueChanged回调，但他的触发时机是值发生时变化时。在OnNetworkSpawn执行时，可能出现
        //NetworkVariable已经有值（从服务器同步过来了）不会触发，本地视觉还是默认值，没更新。如果刚被赋值就会触发
        ApplyCharacter(characterIndex.Value);
        ApplyWeapon(weaponIndex.Value);
        ApplyHealth();
        ApplyLocomotion();
    }

    /// <summary>
    /// 网络对象销毁时 取消所有事件订阅，清理引用
    /// </summary>
    public override void OnNetworkDespawn()
    {
        characterIndex.OnValueChanged -= OnCharacterIndexChanged;
        weaponIndex.OnValueChanged -= OnWeaponIndexChanged;
        currentHealth.OnValueChanged -= OnCurrentHealthChanged;
        maxHealth.OnValueChanged -= OnMaxHealthChanged;
        moveSpeed.OnValueChanged -= OnMoveSpeedChanged;
        grounded.OnValueChanged -= OnGroundedChanged;

        if (partyController != null)
        {
            partyController.ActiveMemberChanged -= OnLocalCharacterChanged;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= OnLocalWeaponEquipped;
            weaponController.AttackStarted -= OnLocalAttack;
        }

        if (playerController != null)
        {
            playerController.JumpStarted -= OnLocalJump;
            playerController.DodgeStarted -= OnLocalDodge;
        }
    }

    /// <summary>
    /// 如果是本机玩家，每隔locomotionSendInterval秒，把本地移动速度和是否在地面这两个数据发给服务器
    /// </summary>
    private void Update()
    {
        //IsSpawned是指这个NetworkObject是否已经在网络上生成完成，在update里操作前先检查，防止对象还没生成就被调用
        if (!IsSpawned || !IsOwner || Time.unscaledTime < nextLocomotionSendTime)
        {
            return;
        }

        nextLocomotionSendTime = Time.unscaledTime + locomotionSendInterval;

        float speed = playerController.NormalizedMoveSpeed;
        bool isGrounded = playerController.IsGrounded;

        //如果自己就是服务器，直接更新数据，如果不是服务器，就把数据发个请求给服务器
        //因为在NGO中Host既是服务器又是客户端，所以Host房主IsServer的值为true，自己就是服务器，直接写入NetworkVariable不需要走网络请求
        if (IsServer)
        {
            SetLocomotionOnServer(speed, isGrounded);
        }
        else
        {
            SubmitLocomotionServerRpc(speed, isGrounded);
        }
    }

    /// <summary>
    /// 初始化服务端状态，服务器给所有NetworkVariable设置初始值
    /// </summary>
    private void InitializeServerState()
    {
        CharacterDefinition firstCharacter = partyController.GetMember(0);

        characterIndex.Value = 0;

        int initialMax = firstCharacter != null ? firstCharacter.maxHealth : 100;

        maxHealth.Value = Mathf.Max(1, initialMax);
        currentHealth.Value = maxHealth.Value;
        weaponIndex.Value = 0;
        moveSpeed.Value = 0f;
        grounded.Value = true;
    }

    /// <summary>
    /// 本机玩家在本地切换了角色 → 如果是本机玩家，把请求发给服务器
    /// </summary>
    /// <param name="definition"></param>
    private void OnLocalCharacterChanged( CharacterDefinition definition)
    {
        //如果不是本机玩家或正在应用网络状态时，暂时屏蔽本地事件响应
        if (!IsOwner || applyingNetworkState)
        {
            return;
        }

        int requestedIndex = partyController.ActiveIndex;

        if (IsServer)
        {
            SetCharacterOnServer(requestedIndex);
        }
        else
        {
            RequestCharacterServerRpc(requestedIndex);
        }
    }

    /// <summary>
    /// 客户端请求切换角色，告诉服务器我想换成第N个角色
    /// </summary>
    /// <param name="requestedIndex"></param>
    //调用方是客户端，执行方是服务器。也就是客户端向服务器请求做某件事
    [ServerRpc]
    private void RequestCharacterServerRpc(int requestedIndex)
    {
        SetCharacterOnServer(requestedIndex);
    }

    /// <summary>
    /// 服务器实际执行角色切换，修改characterIndex这个NetworkVariable
    /// </summary>
    /// <param name="requestedIndex"></param>
    private void SetCharacterOnServer(int requestedIndex)
    {
        CharacterDefinition definition = partyController.GetMember(requestedIndex);

        if (definition == null)
        {
            return;
        }
        //服务器执行逻辑并修改NetworkVariable，NetworkVariable自动同步给所有客户端，所有客户端通过onValueChangef自动应用结果
        characterIndex.Value = requestedIndex;
        maxHealth.Value = Mathf.Max(1, definition.maxHealth);
        currentHealth.Value = maxHealth.Value;
    }

    /// <summary>
    /// 本机玩家在本地换了武器 → 如果是本机玩家，把请求发给服务器
    /// </summary>
    /// <param name="definition"></param>
    private void OnLocalWeaponEquipped(WeaponDefinition definition)
    {
        if (!IsOwner || applyingNetworkState || definition == null)
        {
            return;
        }

        int requestedIndex = FindWeaponIndex(definition);
        if (requestedIndex < 0)
        {
            Debug.LogWarning( "当前武器没有配置到 NetworkPlayerState.weapons。",this);
            return;
        }

        if (IsServer)
        {
            SetWeaponOnServer(requestedIndex);
        }
        else
        {
            RequestWeaponServerRpc(requestedIndex);
        }
    }

    /// <summary>
    /// 客户端请求切换武器，告诉服务器我想换成第N把武器
    /// </summary>
    /// <param name="requestedIndex"></param>
    [ServerRpc]//服务器受到请求，修改NetworkVariable
    private void RequestWeaponServerRpc(int requestedIndex)
    {
        SetWeaponOnServer(requestedIndex);
    }

    /// <summary>
    /// 服务器实际执行武器切换，修改weaponIndex这个NetworkVariable
    /// </summary>
    /// <param name="requestedIndex"></param>
    private void SetWeaponOnServer(int requestedIndex)
    {
        if (weapons == null ||
            requestedIndex < 0 ||
            requestedIndex >= weapons.Length ||
            weapons[requestedIndex] == null)
        {
            return;
        }

        //修改NetworkVariable，NetworkVariable自动同步到所有客户端，所有客户端的OnWeaponIndexChanged自动被触发
        weaponIndex.Value = requestedIndex;
    }

    /// <summary>
    /// 客户端每帧发送移动速度和是否在地面给服务器
    /// </summary>
    /// <param name="requestedSpeed"></param>
    /// <param name="requestedGrounded"></param>
    [ServerRpc]
    private void SubmitLocomotionServerRpc(float requestedSpeed, bool requestedGrounded)
    {
        SetLocomotionOnServer( requestedSpeed, requestedGrounded);
    }

    /// <summary>
    /// 服务器实际执行移动数据更新，修改moveSpeed和grounded这两个NetworkVariable
    /// </summary>
    /// <param name="speed"></param>
    /// <param name="isGrounded"></param>
    private void SetLocomotionOnServer(float speed, bool isGrounded)
    {
        moveSpeed.Value = Mathf.Clamp01(speed);
        grounded.Value = isGrounded;
    }

    /// <summary>
    /// 本机按了跳跃键 → 调用 SendActionToServer 告诉服务器
    /// </summary>
    private void OnLocalJump()
    {
        SendActionToServer(1, 0);
    }

    /// <summary>
    /// 本机按了闪避键 → 调用 SendActionToServer 告诉服务器
    /// </summary>
    private void OnLocalDodge()
    {
        SendActionToServer(2, 0);
    }

    /// <summary>
    /// 本机按了攻击键 → 调用 SendActionToServer 告诉服务器
    /// </summary>
    /// <param name="type"></param>
    private void OnLocalAttack(WeaponType type)
    {
        SendActionToServer(3, (int)type);
    }

    /// <summary>
    /// 上面三个的统一出口：判断如果自己是服务器就直接广播，否则发 ReportActionServerRpc 给服务器
    /// </summary>
    /// <param name="action">byte只占一个字节，而一个字符串或枚举在序列化后可能占用几十个字节，对于高频网络包来说，这种优化很关键，然后这个参数代表动作类型1跳跃2闪避3攻击</param>
    /// <param name="argument">int类型是附带数据，攻击时传WeaponType，跳跃/闪避时传0 无意义</param>
    private void SendActionToServer(byte action, int argument)
    {
        if (!IsOwner || !IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            PlayActionClientRpc(action, argument);//如果自己就是服务器，Host直接广播
        }
        else
        {
            ReportActionServerRpc(action, argument);//如果自己是Client，Client发给服务器
        }
    }

    /// <summary>
    /// 客户端告诉服务器，我做了跳跃、闪避、攻击这个动作
    /// </summary>
    /// <param name="action"></param>
    /// <param name="argument"></param>
    [ServerRpc]
    private void ReportActionServerRpc(byte action, int argument)
    {
        //服务器收到后先校验action是否合法，防止客户端发送伪造的动作编号
        if (action < 1 || action > 3)
        {
            return;
        }
        //校验通过后，调用PlayActionClientRpc广播给所有客户端
        PlayActionClientRpc(action, argument);
    }

    /// <summary>
    /// 服务器告诉客户端这个玩家做了某个动作，远端玩家播放动画
    /// </summary>
    /// <param name="action"></param>
    /// <param name="argument"></param>
    //调用方是服务器，执行方是所有客户端，用途是服务器向所有客户端通知某件事情发生，这个函数会在所有客户端的所有角色实例上都同时执行
    [ClientRpc]
    private void PlayActionClientRpc(byte action, int argument)
    {
        //发起动作的玩家自己不播放， 因为本地已经即时播放过了，避免重复播放导致动画错乱
        if (IsOwner)
        {
            return;
        }
        //只有其他玩家才会播放这个动画
        switch (action)
        {
            case 1:
                animationController.PlayJump();
                break;
            case 2:
                animationController.PlayDodge();
                break;
            case 3:
                //把argument强制转换成WeaponType枚举之前，先检查这个整数是否对应一个合法的枚举值
                if (System.Enum.IsDefined( typeof(WeaponType), argument))
                {
                    //将argument转换为WeaponType类型
                    animationController.PlayAttack((WeaponType)argument);
                }
                break;
        }
    }

    /// <summary>
    /// 服务器执行扣血，修改currentHealth这个NetworkVariable，然后发送伤害反馈给被攻击者
    /// </summary>
    /// <param name="damage"></param>
    public void ServerTakeDamage(int damage)
    {
        if (!IsServer || damage <= 0 || currentHealth.Value <= 0)
        {
            return;
        }

        int healthBefore = currentHealth.Value;

        currentHealth.Value = Mathf.Max(0,currentHealth.Value - damage);

        int appliedDamage = healthBefore - currentHealth.Value;
        if (appliedDamage <= 0)
        {
            return;
        }

        //这是一个定向ClientRpc——只把消息发给指定的客户端，而不是广播给所有人
        //默认情况下[ClientRpc]会广播给所有客户端，但有些信息只需要让特定玩家知道。
        //ClientRpcParams是一个结构体，用于给[ClientRpc]方法传递额外参数，send表示发送相关参数，
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                //TargetClientIds指定要接收这个RPC的客户端ID列表，new[]{OwnerClientId}创建一个只有元素的数组，里面放的是这个角色的主人的客户端ID
                //也就是说PlayerOwnerDamageFeedbackClientRpc这个Rpc只发给OwnerClientID对应的那个客户端，其他人不会受到这条消息
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        PlayOwnerDamageFeedbackClientRpc(appliedDamage, rpcParams);
    }

    /// <summary>
    /// 服务器执行加血，修改currenthealth这个NetworkVariable
    /// </summary>
    /// <param name="amount"></param>
    public void ServerHeal(int amount)
    {
        if (!IsServer || amount <= 0 || currentHealth.Value <= 0)
        {
            return;
        }

        currentHealth.Value = Mathf.Min(maxHealth.Value, currentHealth.Value + amount);
    }

    /// <summary>
    /// 服务器从存档恢复血量，修改currenthealth
    /// </summary>
    /// <param name="savedHealth"></param>
    public void ServerRestoreHealth(int savedHealth)
    {
        if (!IsServer)
        {
            return;
        }

        currentHealth.Value = Mathf.Clamp( savedHealth, 1,maxHealth.Value);
    }

    /// <summary>
    /// 服务器那边角色变了 → 本地调用 ApplyCharacter 切换到对应的模型
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="current"></param>
    private void OnCharacterIndexChanged(int previous, int current)
    {
        ApplyCharacter(current);
    }

    /// <summary>
    /// 服务器那边武器变了 → 本地调用 ApplyWeapon 切换到对应的武器模型
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="current"></param>
    private void OnWeaponIndexChanged(int previous, int current)
    {
        ApplyWeapon(current);
    }

    /// <summary>
    /// 服务器那边血量变了 → 本地调用 ApplyHealth 更新 UI 血条
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="current"></param>
    private void OnCurrentHealthChanged(int previous, int current)
    {
        ApplyHealth();
    }

    /// <summary>
    /// 服务器那边最大血量变了 → 本地调用 ApplyHealth 更新 UI 血条
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="current"></param>
    private void OnMaxHealthChanged(int previous, int current)
    {
        ApplyHealth();
    }

    /// <summary>
    /// 服务器那边移动速度变了 → 本地调用 ApplyLocomotion 更新动画（仅远端玩家）
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="current"></param>
    private void OnMoveSpeedChanged(float previous, float current)
    {
        ApplyLocomotion();
    }

    /// <summary>
    /// 服务器那边是否在地面变了 → 本地调用 ApplyLocomotion 更新动画（仅远端玩家）
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="current"></param>
    private void OnGroundedChanged(bool previous, bool current)
    {
        ApplyLocomotion();
    }

    /// <summary>
    /// 在本地切换到指定索引的角色模型，然后重新应用一次血量
    /// </summary>
    /// <param name="index"></param>
    private void ApplyCharacter(int index)
    {
        if (partyController.GetMember(index) == null || (partyController.ActiveIndex == index && partyController.ActiveMember != null))
        {
            return;
        }

        applyingNetworkState = true;
        partyController.SwitchTo(index);
        applyingNetworkState = false;

        //切换后角色血量可能变化，重新应用一次
        ApplyHealth();
    }

    /// <summary>
    /// 在本地切换到指定索引的武器模型
    /// </summary>
    /// <param name="index"></param>
    private void ApplyWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length || weapons[index] == null || weaponController.CurrentWeapon == weapons[index])
        {
            return;
        }

        applyingNetworkState = true;
        weaponController.Equip(weapons[index]);
        applyingNetworkState = false;
    }

    /// <summary>
    /// 把 currentHealth 和 maxHealth 的值应用到本地 Health 组件上，更新血条
    /// </summary>
    private void ApplyHealth()
    {
        health.ApplyNetworkState(
            currentHealth.Value,
            maxHealth.Value);
    }

    /// <summary>
    /// 把 moveSpeed 和 grounded 的值应用到本地 AnimationController 上，驱动动画（仅远端玩家）
    /// </summary>
    private void ApplyLocomotion()
    {
        if (IsOwner)
        {
            return;
        }

        animationController.ApplyNetworkLocomotion( moveSpeed.Value, grounded.Value);
    }


    /// <summary>
    /// 工具函数：根据 WeaponDefinition 找到它在 weapons 数组里的索引位置
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    private int FindWeaponIndex(WeaponDefinition definition)
    {
        if (weapons == null)
        {
            return -1;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == definition)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 服务器告诉被打的那个人，你被打了，扣了多少血，触发屏幕闪红等效果
    /// </summary>
    /// <param name="appliedDamage"></param>
    /// <param name="rpcParams"></param>
    [ClientRpc]
    private void PlayOwnerDamageFeedbackClientRpc(int appliedDamage,ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || appliedDamage <= 0)
        {
            return;
        }

        // 这里只发布表现，不会再次扣血。
        CombatService.PublishNetworkDamage(this, appliedDamage);
    }
}