using Unity.Netcode;
using UnityEngine;

/// <summary>
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
    [SerializeField] private float locomotionSendInterval = 0.1f;

    private readonly NetworkVariable<int> characterIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> weaponIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

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

    private bool applyingNetworkState;
    private float nextLocomotionSendTime;

    public int CurrentHealth => currentHealth.Value;
    public int MaxHealth => maxHealth.Value;
    public bool IsDead => currentHealth.Value <= 0;

    // 第7册服务器战斗校验使用。
    public int CurrentWeaponIndex => weaponIndex.Value;
    public override void OnNetworkSpawn()
    {
        characterIndex.OnValueChanged += OnCharacterIndexChanged;
        weaponIndex.OnValueChanged += OnWeaponIndexChanged;
        currentHealth.OnValueChanged += OnHealthValueChanged;
        maxHealth.OnValueChanged += OnHealthValueChanged;
        moveSpeed.OnValueChanged += OnMoveSpeedChanged;
        grounded.OnValueChanged += OnGroundedChanged;

        partyController.ActiveMemberChanged += OnLocalCharacterChanged;
        weaponController.WeaponEquipped += OnLocalWeaponEquipped;

        if (IsOwner)
        {
            playerController.JumpStarted += OnLocalJump;
            playerController.DodgeStarted += OnLocalDodge;
            weaponController.AttackStarted += OnLocalAttack;
        }

        animationController.SetUseNetworkLocomotion(!IsOwner);

        if (IsServer)
        {
            InitializeServerState();
        }

        // NetworkVariable 的当前值可能在 OnNetworkSpawn 前已经同步完成，
        // 因此不能只依赖 OnValueChanged，必须主动应用一次。
        ApplyCharacter(characterIndex.Value);
        ApplyWeapon(weaponIndex.Value);
        ApplyHealth();
        ApplyLocomotion();
    }

    public override void OnNetworkDespawn()
    {
        characterIndex.OnValueChanged -= OnCharacterIndexChanged;
        weaponIndex.OnValueChanged -= OnWeaponIndexChanged;
        currentHealth.OnValueChanged -= OnHealthValueChanged;
        maxHealth.OnValueChanged -= OnHealthValueChanged;
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

    private void Update()
    {
        if (!IsSpawned || !IsOwner ||
            Time.unscaledTime < nextLocomotionSendTime)
        {
            return;
        }

        nextLocomotionSendTime =
            Time.unscaledTime + locomotionSendInterval;

        float speed = playerController.NormalizedMoveSpeed;
        bool isGrounded = playerController.IsGrounded;

        if (IsServer)
        {
            SetLocomotionOnServer(speed, isGrounded);
        }
        else
        {
            SubmitLocomotionServerRpc(speed, isGrounded);
        }
    }

    private void InitializeServerState()
    {
        CharacterDefinition firstCharacter =
            partyController.GetMember(0);

        characterIndex.Value = 0;

        int initialMax = firstCharacter != null
            ? firstCharacter.maxHealth
            : 100;

        maxHealth.Value = Mathf.Max(1, initialMax);
        currentHealth.Value = maxHealth.Value;
        weaponIndex.Value = 0;
        moveSpeed.Value = 0f;
        grounded.Value = true;
    }

    private void OnLocalCharacterChanged(
        CharacterDefinition definition)
    {
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

    [ServerRpc]
    private void RequestCharacterServerRpc(int requestedIndex)
    {
        SetCharacterOnServer(requestedIndex);
    }

    private void SetCharacterOnServer(int requestedIndex)
    {
        CharacterDefinition definition =
            partyController.GetMember(requestedIndex);

        if (definition == null)
        {
            return;
        }

        characterIndex.Value = requestedIndex;
        maxHealth.Value = Mathf.Max(1, definition.maxHealth);
        currentHealth.Value = maxHealth.Value;
    }

    private void OnLocalWeaponEquipped(WeaponDefinition definition)
    {
        if (!IsOwner || applyingNetworkState || definition == null)
        {
            return;
        }

        int requestedIndex = FindWeaponIndex(definition);
        if (requestedIndex < 0)
        {
            Debug.LogWarning(
                "当前武器没有配置到 NetworkPlayerState.weapons。",
                this);
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

    [ServerRpc]
    private void RequestWeaponServerRpc(int requestedIndex)
    {
        SetWeaponOnServer(requestedIndex);
    }

    private void SetWeaponOnServer(int requestedIndex)
    {
        if (weapons == null ||
            requestedIndex < 0 ||
            requestedIndex >= weapons.Length ||
            weapons[requestedIndex] == null)
        {
            return;
        }

        weaponIndex.Value = requestedIndex;
    }

    [ServerRpc]
    private void SubmitLocomotionServerRpc(
        float requestedSpeed,
        bool requestedGrounded)
    {
        SetLocomotionOnServer(
            requestedSpeed,
            requestedGrounded);
    }

    private void SetLocomotionOnServer(float speed, bool isGrounded)
    {
        moveSpeed.Value = Mathf.Clamp01(speed);
        grounded.Value = isGrounded;
    }

    private void OnLocalJump()
    {
        SendActionToServer(1, 0);
    }

    private void OnLocalDodge()
    {
        SendActionToServer(2, 0);
    }

    private void OnLocalAttack(WeaponType type)
    {
        SendActionToServer(3, (int)type);
    }

    private void SendActionToServer(byte action, int argument)
    {
        if (!IsOwner || !IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            PlayActionClientRpc(action, argument);
        }
        else
        {
            ReportActionServerRpc(action, argument);
        }
    }

    [ServerRpc]
    private void ReportActionServerRpc(byte action, int argument)
    {
        // 本册只允许三个已知动作，避免客户端发送任意编号。
        if (action < 1 || action > 3)
        {
            return;
        }

        PlayActionClientRpc(action, argument);
    }

    [ClientRpc]
    private void PlayActionClientRpc(byte action, int argument)
    {
        // 拥有者本地已经即时播放过，不能再播放一次。
        if (IsOwner)
        {
            return;
        }

        switch (action)
        {
            case 1:
                animationController.PlayJump();
                break;
            case 2:
                animationController.PlayDodge();
                break;
            case 3:
                if (System.Enum.IsDefined(
                        typeof(WeaponType),
                        argument))
                {
                    animationController.PlayAttack(
                        (WeaponType)argument);
                }
                break;
        }
    }

    /// <summary>
    /// 后续怪物和PVP战斗只能在服务器调用这个方法。
    /// </summary>
    public void ServerTakeDamage(int damage)
    {
        if (!IsServer || damage <= 0 || currentHealth.Value <= 0)
        {
            return;
        }

        currentHealth.Value = Mathf.Max(
            0,
            currentHealth.Value - damage);
    }

    public void ServerHeal(int amount)
    {
        if (!IsServer || amount <= 0 || currentHealth.Value <= 0)
        {
            return;
        }

        currentHealth.Value = Mathf.Min(
            maxHealth.Value,
            currentHealth.Value + amount);
    }

    private void OnCharacterIndexChanged(int previous, int current)
    {
        ApplyCharacter(current);
    }

    private void OnWeaponIndexChanged(int previous, int current)
    {
        ApplyWeapon(current);
    }

    private void OnHealthValueChanged(int previous, int current)
    {
        ApplyHealth();
    }

    private void OnMoveSpeedChanged(float previous, float current)
    {
        ApplyLocomotion();
    }

    private void OnGroundedChanged(bool previous, bool current)
    {
        ApplyLocomotion();
    }

    private void ApplyCharacter(int index)
    {
        if (partyController.GetMember(index) == null ||
            (partyController.ActiveIndex == index &&
             partyController.ActiveMember != null))
        {
            return;
        }

        applyingNetworkState = true;
        partyController.SwitchTo(index);
        applyingNetworkState = false;

        // SwitchTo 会按角色配置重设本地 Health，随后用服务器值覆盖。
        ApplyHealth();
    }

    private void ApplyWeapon(int index)
    {
        if (weapons == null ||
            index < 0 ||
            index >= weapons.Length ||
            weapons[index] == null ||
            weaponController.CurrentWeapon == weapons[index])
        {
            return;
        }

        applyingNetworkState = true;
        weaponController.Equip(weapons[index]);
        applyingNetworkState = false;
    }

    private void ApplyHealth()
    {
        health.ApplyNetworkState(
            currentHealth.Value,
            maxHealth.Value);
    }

    private void ApplyLocomotion()
    {
        if (IsOwner)
        {
            return;
        }

        animationController.ApplyNetworkLocomotion(
            moveSpeed.Value,
            grounded.Value);
    }

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
}