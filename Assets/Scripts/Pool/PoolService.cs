using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolService : MonoBehaviour
{
    [SerializeField] private Transform poolRoot;

    private readonly Dictionary<int, Queue<Component>> available =
        new Dictionary<int, Queue<Component>>();

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        int poolKey = prefab.GetInstanceID();

        if (!available.TryGetValue(poolKey, out Queue<Component> queue))
        {
            queue = new Queue<Component>();
            available.Add(poolKey, queue);
        }

        T instance;

        if (queue.Count > 0)
        {
            instance = queue.Dequeue() as T;
        }
        else
        {
            instance = Instantiate(prefab, poolRoot);
            PooledInstance pooledInstance = instance.GetComponent<PooledInstance>();
            if (pooledInstance == null)
            {
                pooledInstance = instance.gameObject.AddComponent<PooledInstance>();
            }

            pooledInstance.Initialize(poolKey);
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);

        if (instance is IPoolable poolable)
        {
            poolable.OnSpawned();
        }

        return instance;
    }

    public void Despawn(Component instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledInstance pooledInstance = instance.GetComponent<PooledInstance>();
        if (pooledInstance == null)
        {
            // 不属于对象池的实例保持原有行为，便于逐步迁移。
            Destroy(instance.gameObject);
            return;
        }

        if (instance is IPoolable poolable)
        {
            poolable.OnDespawned();
        }

        instance.gameObject.SetActive(false);

        if (!available.TryGetValue(pooledInstance.PoolKey, out Queue<Component> queue))
        {
            queue = new Queue<Component>();
            available.Add(pooledInstance.PoolKey, queue);
        }

        queue.Enqueue(instance);
    }
}