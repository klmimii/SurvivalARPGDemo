using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PooledInstance : MonoBehaviour
{
    public int PoolKey { get; private set; }

    public void Initialize(int poolKey)
    {
        PoolKey = poolKey;
    }
}