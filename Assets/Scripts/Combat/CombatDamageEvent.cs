using UnityEngine;

public readonly struct CombatDamageEvent
{
    public Component TargetComponent { get; }
    public CombatantIdentity TargetIdentity { get; }
    public Vector3 WorldPosition { get; }
    public int RequestedDamage { get; }
    public int AppliedDamage { get; }

    public CombatDamageEvent(
        Component targetComponent,
        CombatantIdentity targetIdentity,
        Vector3 worldPosition,
        int requestedDamage,
        int appliedDamage)
    {
        TargetComponent = targetComponent;
        TargetIdentity = targetIdentity;
        WorldPosition = worldPosition;
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
    }
}