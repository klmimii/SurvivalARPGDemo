using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour, IPoolable
{
    [SerializeField] private float lifetime = 5f;

    [Tooltip("速度很小时不再更新箭头方向，避免LookRotation收到零向量。")]
    [SerializeField] private float minRotateSpeed = 0.1f;

    private int damage;
    private LayerMask targetLayers;
    private Rigidbody body;
    private float despawnTime;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 让箭头始终沿抛物线切线方向旋转。
        if (body.velocity.sqrMagnitude >
            minRotateSpeed * minRotateSpeed)
        {
            transform.rotation =
                Quaternion.LookRotation(body.velocity.normalized);
        }

        if (Time.time >= despawnTime)
        {
            ReturnToPool();
        }
    }

    /// <summary>
    /// 根据目标点、固定箭速和Physics.gravity计算低弧线初速度。
    /// </summary>
    public void LaunchBallistic(
        Vector3 targetPosition,
        float speed,
        int projectileDamage,
        LayerMask layers)
    {
        damage = projectileDamage;
        targetLayers = layers;
        hasHit = false;
        despawnTime = Time.time + lifetime;

        body.useGravity = true;

        if (!TryCalculateBallisticVelocity(
                targetPosition,
                speed,
                out Vector3 initialVelocity))
        {
            // 目标超出当前速度的物理射程时，退回普通方向发射。
            // 仍然受重力影响，所以会下坠，但不保证命中准星。
            Vector3 fallbackDirection =
                targetPosition - transform.position;

            initialVelocity = fallbackDirection.sqrMagnitude > 0.001f
                ? fallbackDirection.normalized * speed
                : transform.forward * speed;

            Debug.LogWarning(
                "箭矢目标超出当前速度可到达范围，已使用普通重力发射。",
                this);
        }

        body.velocity = initialVelocity;

        if (initialVelocity.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(initialVelocity.normalized);
        }
    }

    private bool TryCalculateBallisticVelocity(
        Vector3 targetPosition,
        float speed,
        out Vector3 velocity)
    {
        velocity = Vector3.zero;

        Vector3 displacement =
            targetPosition - transform.position;

        Vector3 horizontalDisplacement =
            new Vector3(displacement.x, 0f, displacement.z);

        float horizontalDistance =
            horizontalDisplacement.magnitude;

        float verticalDistance = displacement.y;
        float gravity = Mathf.Abs(Physics.gravity.y);

        if (horizontalDistance <= 0.001f ||
            gravity <= 0.001f ||
            speed <= 0.001f)
        {
            return false;
        }

        float speedSquared = speed * speed;
        float discriminant =
            speedSquared * speedSquared
            - gravity *
            (gravity * horizontalDistance * horizontalDistance
             + 2f * verticalDistance * speedSquared);

        // 小于0表示以当前速度不存在能够到达目标的弹道。
        if (discriminant < 0f)
        {
            return false;
        }

        // 使用减号得到低弧线；改成加号会得到高抛轨迹。
        float tanTheta =
            (speedSquared - Mathf.Sqrt(discriminant))
            / (gravity * horizontalDistance);

        float cosTheta =
            1f / Mathf.Sqrt(1f + tanTheta * tanTheta);

        float sinTheta = tanTheta * cosTheta;

        Vector3 horizontalDirection =
            horizontalDisplacement.normalized;

        velocity =
            horizontalDirection * (speed * cosTheta)
            + Vector3.up * (speed * sinTheta);

        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit ||
            (targetLayers.value &
             (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        if (CombatService.TryDealDamage(other, damage))
        {
            hasHit = true;
            ReturnToPool();
        }
    }

    public void OnSpawned()
    {
        hasHit = false;
        body.useGravity = false;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    public void OnDespawned()
    {
        body.useGravity = false;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void ReturnToPool()
    {
        if (GameBootstrap.Pool != null)
        {
            GameBootstrap.Pool.Despawn(this);
        }
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//[RequireComponent(typeof(Rigidbody))]
//public class Projectile : MonoBehaviour, IPoolable
//{
//    [SerializeField] private float lifetime = 3f;

//    private int damage;
//    private LayerMask targetLayers;
//    private Rigidbody body;
//    private float despawnTime;
//    private bool hasHit;

//    private void Awake()
//    {
//        body = GetComponent<Rigidbody>();
//    }

//    private void Update()
//    {
//        if (Time.time >= despawnTime)
//        {
//            ReturnToPool();
//        }
//    }

//    public void Launch(Vector3 direction, float speed, int projectileDamage, LayerMask layers)
//    {
//        damage = projectileDamage;
//        targetLayers = layers;
//        hasHit = false;
//        despawnTime = Time.time + lifetime;

//        // 大多数 Unity LTS 版本使用 velocity；若你的版本提供 linearVelocity，也可使用它。
//        body.velocity = direction.normalized * speed;
//    }

//    private void OnTriggerEnter(Collider other)
//    {
//        if (hasHit || (targetLayers.value & (1 << other.gameObject.layer)) == 0)
//        {
//            return;
//        }

//        if (CombatService.TryDealDamage(other, damage))
//        {
//            hasHit = true;
//            ReturnToPool();
//        }
//    }

//    public void OnSpawned()
//    {
//        hasHit = false;
//        body.velocity = Vector3.zero;
//        body.angularVelocity = Vector3.zero;
//    }

//    public void OnDespawned()
//    {
//        body.velocity = Vector3.zero;
//        body.angularVelocity = Vector3.zero;
//    }

//    private void ReturnToPool()
//    {
//        if (gameObject.activeSelf)
//        {
//            GameBootstrap.Pool.Despawn(this);
//        }
//    }
//}