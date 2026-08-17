using UnityEngine;

public static class BuildingPlacementValidator
{
    private static readonly Collider[] HitBuffer = new Collider[64];

    public static bool IsAreaFree(
        BuildingDefinition definition,
        Vector3 position,
        Quaternion rotation,
        LayerMask blockingLayers,
        PlacedBuilding ignoredSupport = null)
    {
        if (definition == null)
        {
            return false;
        }

        Vector3 worldCenter =
            position + rotation * definition.boundsCenter;

        Vector3 halfExtents = definition.boundsSize * 0.5f;
        float skin = definition.boundsSkin;

        halfExtents.x = Mathf.Max(0.01f, halfExtents.x - skin);
        halfExtents.y = Mathf.Max(0.01f, halfExtents.y - skin);
        halfExtents.z = Mathf.Max(0.01f, halfExtents.z - skin);

        int hitCount = Physics.OverlapBoxNonAlloc(
            worldCenter,
            halfExtents,
            HitBuffer,
            rotation,
            blockingLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = HitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            PlacedBuilding hitBuilding =
                hit.GetComponentInParent<PlacedBuilding>();

            if (ignoredSupport != null && hitBuilding == ignoredSupport)
            {
                continue;
            }

            return false;
        }

        return true;
    }
}