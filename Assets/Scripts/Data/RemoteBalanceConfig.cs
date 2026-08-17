using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Survival ARPG/Remote Balance Config", fileName = "RemoteBalanceConfig")]
public class RemoteBalanceConfig : ScriptableObject
{
    [Range(0.5f, 2f)] public float bossHealthMultiplier = 1f;
    [Range(0.5f, 2f)] public float bossDamageMultiplier = 1f;
    [TextArea] public string updateNote;
}
