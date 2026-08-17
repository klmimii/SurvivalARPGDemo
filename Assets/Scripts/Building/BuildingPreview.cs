using UnityEngine;

public class BuildingPreview : MonoBehaviour
{
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    private bool? lastState;

    private void Reset()
    {
        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public void SetPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    public void SetValid(bool isValid)
    {
        if (lastState == isValid)
        {
            return;
        }

        lastState = isValid;
        Material targetMaterial = isValid
            ? validMaterial
            : invalidMaterial;

        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = targetMaterial;
            }
        }
    }
}