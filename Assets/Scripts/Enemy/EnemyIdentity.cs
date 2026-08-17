using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyIdentity : MonoBehaviour
{
    [SerializeField] private string enemyId = "slime";
    public string EnemyId => enemyId;
}
