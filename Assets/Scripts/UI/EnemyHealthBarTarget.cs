using UnityEngine;

[RequireComponent(typeof(Health))]
public class EnemyHealthBarTarget : MonoBehaviour
{
    [SerializeField] private Transform anchor;

    [Min(0.1f)]
    [SerializeField] private float proximityDistance = 7f;

    [Min(0f)]
    [SerializeField] private float damagedVisibleDuration = 4f;

    private Health health;
    private int lastHealth;
    private float damagedVisibleUntil;
    private bool registered;

    public Health Health => health;
    public Vector3 AnchorPosition => anchor != null
        ? anchor.position
        : transform.position + Vector3.up * 2f;

    private void Awake()
    {
        health = GetComponent<Health>();
        lastHealth = health.CurrentHealth;
    }

    private void OnEnable()
    {
        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDied;

        lastHealth = health.CurrentHealth;
        damagedVisibleUntil = 0f;
        TryRegister();
    }

    private void Start()
    {
        // 保证GameBootstrap的Awake已经执行。
        TryRegister();
    }

    private void OnDisable()
    {
        health.HealthChanged -= HandleHealthChanged;
        health.Died -= HandleDied;

        if (registered && GameBootstrap.CombatOverlay != null)
        {
            GameBootstrap.CombatOverlay.UnregisterTarget(this);
        }

        registered = false;
    }

    public bool ShouldDisplay(Vector3 playerPosition, float time)
    {
        if (health == null || health.IsDead)
        {
            return false;
        }

        float sqrDistance = (playerPosition - transform.position).sqrMagnitude;

        bool playerIsNear = sqrDistance <= proximityDistance * proximityDistance;

        return playerIsNear || time < damagedVisibleUntil;
    }

    private void TryRegister()
    {
        if (registered || GameBootstrap.CombatOverlay == null)
        {
            return;
        }

        GameBootstrap.CombatOverlay.RegisterTarget(this);
        registered = true;

    }

    private void HandleHealthChanged(int current, int max)
    {
        if (current < lastHealth && current > 0)
        {
            damagedVisibleUntil =
                Time.time + damagedVisibleDuration;
        }

        lastHealth = current;
    }

    private void HandleDied()
    {
        if (GameBootstrap.CombatOverlay != null)
        {
            GameBootstrap.CombatOverlay.HideTarget(this);
        }
    }


}