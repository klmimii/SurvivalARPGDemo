using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Health))]
public class EnemyWorldHealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject barRoot;
    [SerializeField] private Transform barFacingRoot;
    [SerializeField] private Image healthFillImage;

    [Header("显示条件")]
    [Min(0.1f)]
    [SerializeField] private float proximityDistance = 7f;

    [Tooltip("受到伤害后，即使玩家离开近距离，仍保持显示的时间。")]
    [Min(0f)]
    [SerializeField] private float damagedVisibleDuration = 4f;

    [Tooltip("距离检测间隔；不用每帧都计算距离。")]
    [Min(0.02f)]
    [SerializeField] private float distanceCheckInterval = 0.1f;

    private Health health;
    private Transform player;
    private Camera gameplayCamera;

    private int lastHealth;
    private float damagedVisibleUntil;
    private float nextDistanceCheckTime;
    private bool playerIsNear;

    private void Awake()
    {
        health = GetComponent<Health>();
        gameplayCamera = Camera.main;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        lastHealth = health.CurrentHealth;
        RefreshFill(health.CurrentHealth, health.MaxHealth);
        SetVisible(false);
    }

    private void OnEnable()
    {
        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDied;

        lastHealth = health.CurrentHealth;
        damagedVisibleUntil = 0f;
        playerIsNear = false;
        nextDistanceCheckTime = 0f;

        RefreshFill(health.CurrentHealth, health.MaxHealth);
        SetVisible(false);
    }

    private void OnDisable()
    {
        health.HealthChanged -= HandleHealthChanged;
        health.Died -= HandleDied;
    }

    private void Update()
    {
        if (health.IsDead)
        {
            SetVisible(false);
            return;
        }

        if (Time.time >= nextDistanceCheckTime)
        {
            nextDistanceCheckTime =
                Time.time + distanceCheckInterval;

            UpdatePlayerDistance();
        }

        bool shouldShow =
            playerIsNear || Time.time < damagedVisibleUntil;

        SetVisible(shouldShow);
    }

    private void LateUpdate()
    {
        if (!barRoot.activeSelf || gameplayCamera == null)
        {
            return;
        }

        // 让世界空间Canvas保持与相机相同朝向。
        // 玩家绕到怪物另一面时仍然能够正面看到血条。
        barFacingRoot.rotation = gameplayCamera.transform.rotation;
    }

    private void UpdatePlayerDistance()
    {
        if (player == null)
        {
            playerIsNear = false;
            return;
        }

        float sqrDistance =
            (player.position - transform.position).sqrMagnitude;

        playerIsNear = sqrDistance <=
            proximityDistance * proximityDistance;
    }

    private void HandleHealthChanged(int current, int max)
    {
        RefreshFill(current, max);

        // Health下降说明怪物实际受到攻击。
        if (current < lastHealth && current > 0)
        {
            damagedVisibleUntil =
                Time.time + damagedVisibleDuration;
        }

        lastHealth = current;
    }

    private void RefreshFill(int current, int max)
    {
        float percent = max > 0
            ? (float)current / max
            : 0f;

        healthFillImage.fillAmount = Mathf.Clamp01(percent);
    }

    private void SetVisible(bool visible)
    {
        if (barRoot.activeSelf != visible)
        {
            barRoot.SetActive(visible);
        }
    }

    private void HandleDied()
    {
        SetVisible(false);
    }
}