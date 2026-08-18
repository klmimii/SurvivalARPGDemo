using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public sealed class NetworkArrow : NetworkBehaviour
{
    [Min(0.1f)]
    [SerializeField]
    private float lifetime = 5f;

    [SerializeField]
    private LayerMask enemyLayers;

    private Rigidbody body;
    private int damage;
    private NetworkObject attacker;
    private float despawnTime;
    private bool launched;
    private bool hit;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        body.isKinematic = !IsServer;
        body.useGravity = IsServer;
    }

    private void Update()
    {
        if (!IsServer || !launched)
        {
            return;
        }

        if (body.velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation =
                Quaternion.LookRotation(body.velocity.normalized);
        }

        if (Time.time >= despawnTime)
        {
            NetworkObject.Despawn(true);
        }
    }

    public void ServerLaunch(
        Vector3 targetPoint,
        float speed,
        int projectileDamage,
        NetworkObject attackerObject)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        damage = Mathf.Max(1, projectileDamage);
        attacker = attackerObject;
        despawnTime = Time.time + lifetime;
        launched = true;
        hit = false;

        if (!TryCalculateBallisticVelocity(
                targetPoint,
                speed,
                out Vector3 initialVelocity))
        {
            Vector3 direction = targetPoint - transform.position;
            initialVelocity = direction.sqrMagnitude > 0.001f
                ? direction.normalized * speed
                : transform.forward * speed;
        }

        body.velocity = initialVelocity;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || hit ||
            (enemyLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        NetworkEnemyHealth enemy =
            other.GetComponentInParent<NetworkEnemyHealth>();

        if (enemy == null || enemy.IsDead)
        {
            return;
        }

        hit = enemy.ServerTakeDamage(damage, attacker);

        if (hit && IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    private bool TryCalculateBallisticVelocity(
        Vector3 targetPosition,
        float speed,
        out Vector3 velocity)
    {
        velocity = Vector3.zero;
        Vector3 displacement = targetPosition - transform.position;
        Vector3 horizontal = new Vector3(
            displacement.x, 0f, displacement.z);

        float distance = horizontal.magnitude;
        float height = displacement.y;
        float gravity = Mathf.Abs(Physics.gravity.y);

        if (distance <= 0.001f || gravity <= 0.001f || speed <= 0.001f)
        {
            return false;
        }

        float speedSquared = speed * speed;
        float discriminant = speedSquared * speedSquared
            - gravity *
            (gravity * distance * distance + 2f * height * speedSquared);

        if (discriminant < 0f)
        {
            return false;
        }

        float tanTheta =
            (speedSquared - Mathf.Sqrt(discriminant)) /
            (gravity * distance);

        float cosTheta = 1f / Mathf.Sqrt(1f + tanTheta * tanTheta);
        float sinTheta = tanTheta * cosTheta;

        velocity = horizontal.normalized * (speed * cosTheta)
            + Vector3.up * (speed * sinTheta);

        return true;
    }
}