using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class WeaponController : MonoBehaviour
{
    [Header("Combat References")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private PlayerAnimationController animationController;

    [Header("Bow Aim References")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;
    [SerializeField] private GameObject bowCrosshair;

    [Tooltip("准星射线可以命中的层：Enemy、Ground、Obstacle、Building等。不要包含Player。")]
    [SerializeField] private LayerMask aimCollisionLayers;

    [Header("Bow Aim Settings")]
    [Min(0.05f)]
    [SerializeField] private float aimHoldDuration = 0.35f;

    [Min(1f)]
    [SerializeField] private float maxAimDistance = 100f;

    [Min(0f)]
    [SerializeField] private float aimTurnSpeed = 12f;

    public WeaponDefinition CurrentWeapon { get; private set; }

    private float nextAttackTime;
    private WeaponDefinition pendingWeapon;

    private bool bowAttackHeld;
    private bool isAiming;
    private float bowPressedTime;

    // 鼠标松开时记录方向，动画离弦帧再使用。
    //private Vector3 pendingProjectileDirection;
    private Vector3 pendingProjectileTargetPoint;
    private bool hasPendingProjectileTarget;

    /// <summary>武器切换完成后广播，网络状态组件会监听。</summary>
    public event Action<WeaponDefinition> WeaponEquipped;

    /// <summary>本机开始攻击动画时广播，用于通知远端播放同类动画。</summary>
    public event Action<WeaponType> AttackStarted;
    private NetworkPlayerCombat networkCombat;

    private void Awake()
    {
        if (bowCrosshair != null)
        {
            bowCrosshair.SetActive(false);
        }
    }

    private void OnEnable()
    {
        attackAction.action.Enable();

        // 不再订阅performed；started表示按下，canceled表示松开。
        attackAction.action.started += OnAttackStarted;
        attackAction.action.canceled += OnAttackCanceled;
    }

    private void OnDisable()
    {
        attackAction.action.started -= OnAttackStarted;
        attackAction.action.canceled -= OnAttackCanceled;
        attackAction.action.Disable();

        CancelBowInput();
    }

    private void Update()
    {
        if (GameBootstrap.InputMode != null &&
            !GameBootstrap.InputMode.IsGameplay())
        {
            CancelBowInput();
            return;
        }

        if (!bowAttackHeld || isAiming)
        {
            return;
        }

        if (CurrentWeapon == null ||
            CurrentWeapon.weaponType != WeaponType.Bow)
        {
            CancelBowInput();
            return;
        }

        if (Time.time - bowPressedTime >= aimHoldDuration)
        {
            EnterAimMode();
        }
    }

    private void LateUpdate()
    {
        if (!isAiming || gameplayCamera == null)
        {
            return;
        }

        // 瞄准时角色只在水平方向面向镜头前方。
        Vector3 lookDirection = gameplayCamera.transform.forward;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(lookDirection.normalized);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            aimTurnSpeed * Time.deltaTime);
    }

    public void Equip(WeaponDefinition weapon)
    {
        CancelBowInput();

        CurrentWeapon = weapon;
        pendingWeapon = null;
        //pendingProjectileDirection = Vector3.zero;
        hasPendingProjectileTarget = false;
        pendingProjectileTargetPoint = Vector3.zero;
        nextAttackTime = 0f;

        WeaponEquipped?.Invoke(CurrentWeapon);
    }

    private void OnAttackStarted(
        InputAction.CallbackContext context)
    {
        if (!CanStartAttack())
        {
            return;
        }

        if (CurrentWeapon.weaponType == WeaponType.Bow)
        {
            bowAttackHeld = true;
            bowPressedTime = Time.time;
            return;
        }

        // 匕首和长刀仍然按下立即攻击。
        StartAttack();
    }

    private void OnAttackCanceled(
        InputAction.CallbackContext context)
    {
        if (!bowAttackHeld)
        {
            return;
        }

        bowAttackHeld = false;

        // 在退出瞄准镜头前记录当前相机中心指向。
        //pendingProjectileDirection =
        //    CalculateCameraShotDirection();
        pendingProjectileTargetPoint = CalculateCameraAimPoint();

        hasPendingProjectileTarget = true;

        ExitAimMode();

        // 松开时可能已经切到UI或攻击进入冷却，再检查一次。
        if (!CanStartAttack())
        {
            return;
        }

        StartAttack();
    }

    private bool CanStartAttack()
    {
        if (CurrentWeapon == null ||
            pendingWeapon != null ||
            Time.time < nextAttackTime)
        {
            return false;
        }

        if (GameBootstrap.InputMode != null &&
            !GameBootstrap.InputMode.IsGameplay())
        {
            return false;
        }

        return true;
    }

    private void StartAttack()
    {
        pendingWeapon = CurrentWeapon;
        nextAttackTime = Time.time + pendingWeapon.cooldown;

        // 近战不需要投射方向。
        //if (pendingWeapon.weaponType != WeaponType.Bow)
        //{
        //    pendingProjectileDirection = Vector3.zero;
        //}
        //else if (pendingProjectileDirection.sqrMagnitude < 0.001f)
        //{
        //    pendingProjectileDirection =
        //        CalculateCameraShotDirection();
        //}
        if (pendingWeapon.weaponType != WeaponType.Bow)
        {
            hasPendingProjectileTarget = false;
        }
        else if (!hasPendingProjectileTarget)
        {
            pendingProjectileTargetPoint = CalculateCameraAimPoint();

            hasPendingProjectileTarget = true;
        }

        animationController.PlayAttack(pendingWeapon.weaponType);

        AttackStarted?.Invoke(pendingWeapon.weaponType);
    }

    private void EnterAimMode()
    {
        isAiming = true;

        if (bowCrosshair != null)
        {
            bowCrosshair.SetActive(true);
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetAimMode(true);
        }

        animationController.SetAiming(true);
    }

    private void ExitAimMode()
    {
        if (!isAiming)
        {
            return;
        }

        isAiming = false;

        if (bowCrosshair != null)
        {
            bowCrosshair.SetActive(false);
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetAimMode(false);
        }

        animationController.SetAiming(false);
    }

    private void CancelBowInput()
    {
        bowAttackHeld = false;

        // 已经开始播放攻击动画后，需要保留目标点直到离弦帧。
        if (pendingWeapon == null)
        {
            hasPendingProjectileTarget = false;
            pendingProjectileTargetPoint = Vector3.zero;
        }

        ExitAimMode();
    }

    //private void CancelBowInput()
    //{
    //    bowAttackHeld = false;

    //    // 已经松手并开始播放攻击动画时，保留记录好的方向，
    //    // 否则UI在离弦帧前打开会让箭失去目标方向。
    //    if (pendingWeapon == null)
    //    {
    //        pendingProjectileDirection = Vector3.zero;
    //    }

    //    ExitAimMode();
    //}

    ///// <summary>
    ///// 从相机屏幕中心发射射线，再从角色箭矢出生点指向射线目标。
    ///// 这样箭从弓的位置出现，但最终飞向相机看到的目标。
    ///// </summary>
    //private Vector3 CalculateCameraShotDirection()
    //{
    //    if (gameplayCamera == null ||
    //        projectileSpawnPoint == null)
    //    {
    //        return transform.forward;
    //    }

    //    Ray aimRay = gameplayCamera.ViewportPointToRay(
    //        new Vector3(0.5f, 0.5f, 0f));

    //    Vector3 targetPoint;

    //    if (Physics.Raycast(
    //            aimRay,
    //            out RaycastHit hit,
    //            maxAimDistance,
    //            aimCollisionLayers,
    //            QueryTriggerInteraction.Ignore))
    //    {
    //        targetPoint = hit.point;
    //    }
    //    else
    //    {
    //        targetPoint = aimRay.GetPoint(maxAimDistance);
    //    }

    //    Vector3 direction =
    //        targetPoint - projectileSpawnPoint.position;

    //    return direction.sqrMagnitude > 0.001f
    //        ? direction.normalized
    //        : gameplayCamera.transform.forward;
    //}

    private Vector3 CalculateCameraAimPoint()
    {
        if (gameplayCamera == null)
        {
            return projectileSpawnPoint.position
                   + transform.forward * maxAimDistance;
        }

        Ray aimRay = gameplayCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(
                aimRay,
                out RaycastHit hit,
                maxAimDistance,
                aimCollisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return aimRay.GetPoint(maxAimDistance);
    }

    // Attack动画离弦/命中帧调用。
    public void AnimationEvent_AttackHit()
    {
        if (pendingWeapon == null)
        {
            return;
        }

        if (pendingWeapon.weaponType == WeaponType.Bow)
        {
            FireProjectile(
                pendingWeapon,
                pendingProjectileTargetPoint);
        }
        else
        {
            PerformMeleeAttack(pendingWeapon);
        }
        //if (pendingWeapon.weaponType == WeaponType.Bow)
        //{
        //    FireProjectile(
        //        pendingWeapon,
        //        pendingProjectileDirection);
        //}
        //else
        //{
        //    PerformMeleeAttack(pendingWeapon);
        //}
    }

    // Attack动画末尾调用。
    //public void AnimationEvent_AttackFinished()
    //{
    //    pendingWeapon = null;
    //    pendingProjectileDirection = Vector3.zero;
    //}
    public void AnimationEvent_AttackFinished()
    {
        pendingWeapon = null;
        hasPendingProjectileTarget = false;
        pendingProjectileTargetPoint = Vector3.zero;
    }

    private void PerformMeleeAttack(WeaponDefinition weapon)
    {
        if (networkCombat != null && networkCombat.IsSpawned)
        {
            networkCombat.RequestMeleeHit(weapon);
            return;
        }

        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            weapon.attackRadius,
            enemyLayers);

        HashSet<IDamageable> hitTargets =
            new HashSet<IDamageable>();

        foreach (Collider hit in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<IDamageable>();

            if (damageable == null ||
                hitTargets.Contains(damageable))
            {
                continue;
            }

            hitTargets.Add(damageable);
            CombatService.TryDealDamage(hit, weapon.damage);
        }
    }
    private void FireProjectile(
    WeaponDefinition weapon,
    Vector3 targetPoint)
    {
        if (networkCombat != null && networkCombat.IsSpawned)
        {
            networkCombat.RequestBowShot(weapon, targetPoint);
            return;
        }

        if (weapon.projectilePrefab == null)
        {
            Debug.LogWarning(
                "弓箭未配置投射物Prefab。",
                this);
            return;
        }

        Vector3 initialDirection =
            targetPoint - projectileSpawnPoint.position;

        if (initialDirection.sqrMagnitude <= 0.001f)
        {
            initialDirection = projectileSpawnPoint.forward;
        }

        Projectile projectile = GameBootstrap.Pool.Spawn(
            weapon.projectilePrefab,
            projectileSpawnPoint.position,
            Quaternion.LookRotation(initialDirection.normalized));

        projectile.LaunchBallistic(
            targetPoint,
            weapon.projectileSpeed,
            weapon.damage,
            enemyLayers);
    }

    //private void FireProjectile(
    //    WeaponDefinition weapon,
    //    Vector3 direction)
    //{
    //    if (weapon.projectilePrefab == null)
    //    {
    //        Debug.LogWarning(
    //            "弓箭未配置投射物Prefab。",
    //            this);
    //        return;
    //    }

    //    if (direction.sqrMagnitude < 0.001f)
    //    {
    //        direction = projectileSpawnPoint.forward;
    //    }

    //    Quaternion projectileRotation =
    //        Quaternion.LookRotation(direction.normalized);

    //    Projectile projectile = GameBootstrap.Pool.Spawn(
    //        weapon.projectilePrefab,
    //        projectileSpawnPoint.position,
    //        projectileRotation);

    //    projectile.Launch(
    //        direction,
    //        weapon.projectileSpeed,
    //        weapon.damage,
    //        enemyLayers);
    //}

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null || CurrentWeapon == null)
        {
            return;
        }

        if (CurrentWeapon.weaponType == WeaponType.Dagger ||
            CurrentWeapon.weaponType == WeaponType.LongBlade)
        {
            Gizmos.color =
                CurrentWeapon.weaponType == WeaponType.Dagger
                    ? Color.yellow
                    : Color.cyan;

            Gizmos.DrawWireSphere(
                attackPoint.position,
                CurrentWeapon.attackRadius);
        }
    }
    public void SetCameraReferences(Camera cameraValue, ThirdPersonCamera controllerValue)
    {
        gameplayCamera = cameraValue;
        thirdPersonCamera = controllerValue;
    }

    public void SetNetworkCombat(NetworkPlayerCombat value)
    {
        networkCombat = value;
    }

}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.InputSystem;

//public class WeaponController : MonoBehaviour
//{
//    [Header("References")]
//    [SerializeField] private Transform attackPoint;//近战判定中心
//    [SerializeField] private Transform projectileSpawnPoint;//远程子弹生成
//    [SerializeField] private InputActionReference attackAction;//新输入系统的攻击动作
//    [SerializeField] private LayerMask enemyLayers;//放入图层
//    [SerializeField] private PlayerAnimationController animationController;//动画状态机

//    public WeaponDefinition CurrentWeapon { get; private set; }

//    private float nextAttackTime;
//    private WeaponDefinition pendingWeapon;

//    private void OnEnable()
//    {
//        attackAction.action.Enable();
//        attackAction.action.performed += OnAttackPerformed;
//    }

//    private void OnDisable()
//    {
//        attackAction.action.performed -= OnAttackPerformed;
//        attackAction.action.Disable();
//    }

//    public void Equip(WeaponDefinition weapon)
//    {
//        CurrentWeapon = weapon;
//        pendingWeapon = null;
//        nextAttackTime = 0f;
//    }

//    private void OnAttackPerformed(InputAction.CallbackContext context)
//    {
//        if (CurrentWeapon == null || pendingWeapon != null || Time.time < nextAttackTime)
//        {
//            return;
//        }

//        if (GameBootstrap.InputMode != null && !GameBootstrap.InputMode.IsGameplay())
//        {
//            return;
//        }

//        // 锁定这次攻击使用的武器，防止动画过程中按 1/2/3 改武器导致命中结算错误。
//        pendingWeapon = CurrentWeapon;
//        nextAttackTime = Time.time + pendingWeapon.cooldown;
//        animationController.PlayAttack(pendingWeapon.weaponType);
//    }

//    // 由攻击动画 Clip 中的 Animation Event 调用。
//    public void AnimationEvent_AttackHit()
//    {
//        if (pendingWeapon == null)
//        {
//            return;
//        }

//        if (pendingWeapon.weaponType == WeaponType.Bow)
//        {
//            FireProjectile(pendingWeapon);
//        }
//        else
//        {
//            PerformMeleeAttack(pendingWeapon);
//        }
//    }

//    // 由攻击动画末尾的 Animation Event 调用。
//    public void AnimationEvent_AttackFinished()
//    {
//        pendingWeapon = null;
//    }

//    private void PerformMeleeAttack(WeaponDefinition weapon)
//    {
//        Collider[] hits = Physics.OverlapSphere(
//            attackPoint.position,
//            weapon.attackRadius,
//            enemyLayers);

//        HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

//        foreach (Collider hit in hits)
//        {
//            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
//            if (damageable == null || hitTargets.Contains(damageable))
//            {
//                continue;
//            }

//            hitTargets.Add(damageable);
//            CombatService.TryDealDamage(hit, weapon.damage);
//        }
//    }

//    private void FireProjectile(WeaponDefinition weapon)
//    {
//        if (weapon.projectilePrefab == null)
//        {
//            Debug.LogWarning("弓箭未配置投射物 Prefab。", this);
//            return;
//        }

//        Projectile projectile = GameBootstrap.Pool.Spawn(
//    weapon.projectilePrefab,
//    projectileSpawnPoint.position,
//    projectileSpawnPoint.rotation);

//        projectile.Launch(
//            projectileSpawnPoint.forward,
//            weapon.projectileSpeed,
//            weapon.damage,
//            enemyLayers);
//    }
//}

