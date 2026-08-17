using UnityEngine;

public class CameraFeedback : MonoBehaviour
{
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;

    [Header("Strength")]
    [SerializeField] private float enemyHitDuration = 0.07f;
    [SerializeField] private float enemyHitStrength = 0.035f;
    [SerializeField] private float playerHitDuration = 0.14f;
    [SerializeField] private float playerHitStrength = 0.09f;

    private void OnEnable()
    {
        CombatService.DamageApplied += HandleDamageApplied;
    }

    private void OnDisable()
    {
        CombatService.DamageApplied -= HandleDamageApplied;
    }

    private void HandleDamageApplied(CombatDamageEvent damageEvent)
    {
        if (thirdPersonCamera == null ||
            GameBootstrap.InputMode == null ||
            !GameBootstrap.InputMode.IsGameplay())
        {
            return;
        }

        bool playerWasHit = damageEvent.TargetIdentity != null &&
            damageEvent.TargetIdentity.Faction == CombatFaction.Player;

        thirdPersonCamera.Shake(
            playerWasHit ? playerHitDuration : enemyHitDuration,
            playerWasHit ? playerHitStrength : enemyHitStrength);
    }
}