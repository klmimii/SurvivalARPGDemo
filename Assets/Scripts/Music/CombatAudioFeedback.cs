using UnityEngine;

public class CombatAudioFeedback : MonoBehaviour
{
    [SerializeField] private AudioClip hitEnemyClip;
    [SerializeField] private AudioClip playerHurtClip;
    [SerializeField] private float hitVolume = 0.7f;

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
        if (GameBootstrap.Audio == null)
        {
            return;
        }

        bool playerWasHit = damageEvent.TargetIdentity != null &&
            damageEvent.TargetIdentity.Faction == CombatFaction.Player;

        if (playerWasHit)
        {
            GameBootstrap.Audio.PlaySfx2D(playerHurtClip, hitVolume);
            return;
        }

        GameBootstrap.Audio.PlaySfxAt(
            hitEnemyClip,
            damageEvent.WorldPosition,
            hitVolume);
    }
}