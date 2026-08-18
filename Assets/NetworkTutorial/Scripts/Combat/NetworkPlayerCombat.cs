using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerCombat : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private NetworkPlayerState playerState;

    [SerializeField]
    private Transform attackPoint;

    [SerializeField]
    private Transform projectileSpawnPoint;

    [Tooltip("必须和NetworkPlayerState保持相同顺序。")]
    [SerializeField]
    private WeaponDefinition[] weapons;

    [SerializeField]
    private LayerMask networkEnemyLayers;

    [SerializeField]
    private NetworkArrow arrowPrefab;

    [Header("Server Validation")]
    [Min(1f)]
    [SerializeField]
    private float maximumAimDistance = 100f;

    private float nextServerAttackTime;

    public void RequestMeleeHit(WeaponDefinition weapon)
    {
        if (!IsOwner || weapon == null)
        {
            return;
        }

        int index = FindWeaponIndex(weapon);
        if (index >= 0)
        {
            RequestMeleeServerRpc(index);
        }
    }

    public void RequestBowShot(
        WeaponDefinition weapon,
        Vector3 targetPoint)
    {
        if (!IsOwner || weapon == null)
        {
            return;
        }

        int index = FindWeaponIndex(weapon);
        if (index >= 0)
        {
            RequestBowServerRpc(index, targetPoint);
        }
    }

    [ServerRpc]
    private void RequestMeleeServerRpc(int requestedWeaponIndex)
    {
        if (!ServerValidateWeapon(
                requestedWeaponIndex,
                false,
                out WeaponDefinition weapon))
        {
            return;
        }

        nextServerAttackTime = Time.time + weapon.cooldown;

        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            weapon.attackRadius,
            networkEnemyLayers,
            QueryTriggerInteraction.Collide);

        HashSet<NetworkEnemyHealth> damaged =
            new HashSet<NetworkEnemyHealth>();

        foreach (Collider hitCollider in hits)
        {
            NetworkEnemyHealth enemy =
                hitCollider.GetComponentInParent<NetworkEnemyHealth>();

            if (enemy == null || enemy.IsDead || !damaged.Add(enemy))
            {
                continue;
            }

            enemy.ServerTakeDamage(weapon.damage, NetworkObject);
        }
    }

    [ServerRpc]
    private void RequestBowServerRpc(
        int requestedWeaponIndex,
        Vector3 requestedTargetPoint)
    {
        if (!ServerValidateWeapon(
                requestedWeaponIndex,
                true,
                out WeaponDefinition weapon) || arrowPrefab == null)
        {
            return;
        }

        Vector3 toTarget =
            requestedTargetPoint - projectileSpawnPoint.position;

        if (toTarget.sqrMagnitude >
            maximumAimDistance * maximumAimDistance)
        {
            requestedTargetPoint = projectileSpawnPoint.position
                + toTarget.normalized * maximumAimDistance;
        }

        nextServerAttackTime = Time.time + weapon.cooldown;

        NetworkArrow arrow = Instantiate(
            arrowPrefab,
            projectileSpawnPoint.position,
            projectileSpawnPoint.rotation);

        arrow.NetworkObject.Spawn(true);
        arrow.ServerLaunch(
            requestedTargetPoint,
            weapon.projectileSpeed,
            weapon.damage,
            NetworkObject);
    }

    private bool ServerValidateWeapon(
        int requestedWeaponIndex,
        bool requireBow,
        out WeaponDefinition weapon)
    {
        weapon = null;

        if (!IsServer || playerState == null || playerState.IsDead ||
            Time.time < nextServerAttackTime || weapons == null ||
            requestedWeaponIndex < 0 ||
            requestedWeaponIndex >= weapons.Length ||
            playerState.CurrentWeaponIndex != requestedWeaponIndex)
        {
            return false;
        }

        weapon = weapons[requestedWeaponIndex];
        if (weapon == null)
        {
            return false;
        }

        bool isBow = weapon.weaponType == WeaponType.Bow;
        return requireBow == isBow;
    }

    private int FindWeaponIndex(WeaponDefinition weapon)
    {
        if (weapons == null)
        {
            return -1;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == weapon)
            {
                return i;
            }
        }

        return -1;
    }
}