using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossIdentity : MonoBehaviour
{
    [SerializeField] private string bossId = "forest_guardian";
    public string BossId => bossId;
}
