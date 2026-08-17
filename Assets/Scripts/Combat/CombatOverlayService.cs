using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatOverlayService : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private RectTransform healthBarLayer;
    [SerializeField] private RectTransform damageNumberLayer;

    [Header("Prefabs")]
    [SerializeField] private EnemyHealthBarWidget healthBarPrefab;
    [SerializeField] private DamageNumberWidget damageNumberPrefab;

    [Header("World References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform player;

    [Tooltip("只选择墙、地形等遮挡层，不要选择Enemy层。")]
    [SerializeField] private LayerMask occlusionLayers;

    [Header("Pool")]
    [Min(0)]
    [SerializeField] private int healthBarPrewarmCount = 20;

    [Min(0)]
    [SerializeField] private int damageNumberPrewarmCount = 30;

    [Header("Update")]
    [Min(0.02f)]
    [SerializeField] private float visibilityCheckInterval = 0.1f;

    [SerializeField] private float screenMargin = 40f;

    [Header("Colors")]
    [SerializeField]
    private Color enemyDamageColor =
        new Color(1f, 0.82f, 0.2f);

    [SerializeField]
    private Color playerDamageColor =
        new Color(1f, 0.25f, 0.25f);

    private readonly HashSet<EnemyHealthBarTarget> targets =
        new HashSet<EnemyHealthBarTarget>();

    private readonly Dictionary<EnemyHealthBarTarget, EnemyHealthBarWidget>
        activeBars =
            new Dictionary<EnemyHealthBarTarget, EnemyHealthBarWidget>();

    private readonly Stack<EnemyHealthBarWidget> freeBars =
        new Stack<EnemyHealthBarWidget>();

    private readonly Stack<DamageNumberWidget> freeDamageNumbers =
        new Stack<DamageNumberWidget>();

    private readonly List<EnemyHealthBarTarget> releaseScratch =
        new List<EnemyHealthBarTarget>();

    private Action<DamageNumberWidget> releaseDamageCallback;
    private float nextVisibilityCheckTime;

    private void Awake()
    {
        releaseDamageCallback = ReleaseDamageNumber;
        ResolveWorldReferences();
        PrewarmPools();
    }

    private void OnEnable()
    {
        CombatService.DamageApplied += HandleDamageApplied;
    }

    private void OnDisable()
    {
        CombatService.DamageApplied -= HandleDamageApplied;
    }

    private void LateUpdate()
    {
        if (worldCamera == null || player == null)
        {
            ResolveWorldReferences();
        }

        if (worldCamera == null || player == null)
        {
            return;
        }

        if (Time.time >= nextVisibilityCheckTime)
        {
            nextVisibilityCheckTime =
                Time.time + visibilityCheckInterval;
            RefreshVisibleTargets();
        }

        UpdateActiveBarPositions();
    }

    public void RegisterTarget(EnemyHealthBarTarget target)
    {
        if (target != null)
        {
            targets.Add(target);
        }
    }

    public void UnregisterTarget(EnemyHealthBarTarget target)
    {
        if (target == null)
        {
            return;
        }

        targets.Remove(target);
        ReleaseBar(target);
    }

    public void HideTarget(EnemyHealthBarTarget target)
    {
        ReleaseBar(target);
    }

    private void RefreshVisibleTargets()
    {
        releaseScratch.Clear();

        foreach (KeyValuePair<EnemyHealthBarTarget, EnemyHealthBarWidget>
                 pair in activeBars)
        {
            if (pair.Key == null ||
                !ShouldShowTarget(pair.Key))
            {
                releaseScratch.Add(pair.Key);
            }
        }

        foreach (EnemyHealthBarTarget target in releaseScratch)
        {
            ReleaseBar(target);
        }

        foreach (EnemyHealthBarTarget target in targets)
        {
            if (target == null || activeBars.ContainsKey(target))
            {
                continue;
            }

            if (ShouldShowTarget(target))
            {
                AcquireBar(target);
            }
        }
    }

    private bool ShouldShowTarget(EnemyHealthBarTarget target)
    {
        if (!target.ShouldDisplay(player.position, Time.time))
        {
            return false;
        }

        if (occlusionLayers.value == 0)
        {
            return true;
        }

        Vector3 direction =
            target.AnchorPosition - worldCamera.transform.position;
        float distance = direction.magnitude;

        return !Physics.Raycast(
            worldCamera.transform.position,
            direction.normalized,
            distance,
            occlusionLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void UpdateActiveBarPositions()
    {
        releaseScratch.Clear();

        foreach (KeyValuePair<EnemyHealthBarTarget, EnemyHealthBarWidget>
                 pair in activeBars)
        {
            EnemyHealthBarTarget target = pair.Key;
            EnemyHealthBarWidget widget = pair.Value;

            if (target == null || target.Health == null || target.Health.IsDead)
            {
                releaseScratch.Add(target);
                continue;
            }

            bool onScreen = TryWorldToLocalPoint(
                target.AnchorPosition,
                healthBarLayer,
                out Vector2 localPoint);

            widget.SetScreenVisible(onScreen);

            if (onScreen)
            {
                widget.SetLocalPosition(localPoint);
                widget.Refresh(
                    target.Health.CurrentHealth,
                    target.Health.MaxHealth);
            }
        }

        foreach (EnemyHealthBarTarget target in releaseScratch)
        {
            ReleaseBar(target);
        }
    }

    private void HandleDamageApplied(CombatDamageEvent damageEvent)
    {
        ShowDamageNumber(damageEvent);

        EnemyHealthBarTarget target =
            damageEvent.TargetComponent != null
                ? damageEvent.TargetComponent
                    .GetComponentInParent<EnemyHealthBarTarget>()
                : null;

        // 受到攻击时立即借出血条，不必等待下一次0.1秒可见性扫描。
        if (target != null &&
            target.Health != null &&
            !target.Health.IsDead &&
            !activeBars.ContainsKey(target))
        {
            AcquireBar(target);
        }
    }

    private void ShowDamageNumber(CombatDamageEvent damageEvent)
    {
        if (!TryWorldToLocalPoint(
                damageEvent.WorldPosition,
                damageNumberLayer,
                out Vector2 localPoint))
        {
            return;
        }

        DamageNumberWidget widget = freeDamageNumbers.Count > 0
            ? freeDamageNumbers.Pop()
            : CreateDamageNumberWidget();

        Color color = damageEvent.TargetIdentity != null &&
                      damageEvent.TargetIdentity.Faction == CombatFaction.Player
            ? playerDamageColor
            : enemyDamageColor;

        widget.Play(
            localPoint,
            damageEvent.AppliedDamage,
            color,
            releaseDamageCallback);
    }

    private void AcquireBar(EnemyHealthBarTarget target)
    {
        if (target == null || activeBars.ContainsKey(target))
        {
            return;
        }

        EnemyHealthBarWidget widget = freeBars.Count > 0
            ? freeBars.Pop()
            : CreateHealthBarWidget();

        widget.Activate();
        widget.Refresh(
            target.Health.CurrentHealth,
            target.Health.MaxHealth);
        activeBars.Add(target, widget);
    }

    private void ReleaseBar(EnemyHealthBarTarget target)
    {
        // 使用ReferenceEquals而不是Unity重载的== null。
        // 即使目标正在销毁，也仍要从Dictionary移除对应Widget。
        if (ReferenceEquals(target, null) ||
            !activeBars.TryGetValue(target, out EnemyHealthBarWidget widget))
        {
            return;
        }

        activeBars.Remove(target);
        widget.Deactivate();
        freeBars.Push(widget);
    }

    private void ReleaseDamageNumber(DamageNumberWidget widget)
    {
        if (widget == null)
        {
            return;
        }

        widget.Deactivate();
        freeDamageNumbers.Push(widget);
    }

    private bool TryWorldToLocalPoint(
        Vector3 worldPosition,
        RectTransform layer,
        out Vector2 localPoint)
    {
        localPoint = default;
        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);

        if (screenPoint.z <= 0f ||
            screenPoint.x < -screenMargin ||
            screenPoint.y < -screenMargin ||
            screenPoint.x > Screen.width + screenMargin ||
            screenPoint.y > Screen.height + screenMargin)
        {
            return false;
        }

        Camera uiCamera = overlayCanvas.renderMode ==
                          RenderMode.ScreenSpaceOverlay
            ? null
            : overlayCanvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            layer,
            screenPoint,
            uiCamera,
            out localPoint);
    }

    private void ResolveWorldReferences()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
    }

    private void PrewarmPools()
    {
        for (int i = 0; i < healthBarPrewarmCount; i++)
        {
            EnemyHealthBarWidget widget = CreateHealthBarWidget();
            widget.Deactivate();
            freeBars.Push(widget);
        }

        for (int i = 0; i < damageNumberPrewarmCount; i++)
        {
            DamageNumberWidget widget = CreateDamageNumberWidget();
            widget.Deactivate();
            freeDamageNumbers.Push(widget);
        }
    }

    private EnemyHealthBarWidget CreateHealthBarWidget()
    {
        return Instantiate(healthBarPrefab, healthBarLayer);
    }

    private DamageNumberWidget CreateDamageNumberWidget()
    {
        return Instantiate(damageNumberPrefab, damageNumberLayer);
    }
}