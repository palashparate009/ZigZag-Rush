using UnityEngine;

public static class CarVisualUtility
{
    public static void RemoveGameplayComponents(GameObject model)
    {
        if (model == null) return;
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (Rigidbody rigidbody in model.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.detectCollisions = false;
            rigidbody.isKinematic = true;
            Object.Destroy(rigidbody);
        }
        foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
    }

    public static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        return TryGetLocalRendererBounds(root, renderers, out bounds);
    }

    public static bool TryGetLocalRendererBounds(Transform root, Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (root == null || renderers == null) return false;

        Matrix4x4 worldToLocal = root.worldToLocalMatrix;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is TrailRenderer || renderer is ParticleSystemRenderer) continue;
            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 local = worldToLocal.MultiplyPoint3x4(corner);
                if (!found)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    found = true;
                }
                else bounds.Encapsulate(local);
            }
        }
        return found;
    }

    public static void NormalizeToBounds(Transform model, Transform referenceRoot, Bounds targetBounds)
    {
        if (model == null || referenceRoot == null) return;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        if (!TryGetLocalRendererBounds(referenceRoot, model.GetComponentsInChildren<Renderer>(true), out Bounds source)) return;

        if (source.size.x > source.size.z * 1.08f)
        {
            model.localRotation = Quaternion.Euler(0f, 90f, 0f);
            TryGetLocalRendererBounds(referenceRoot, model.GetComponentsInChildren<Renderer>(true), out source);
        }

        float sourceLength = Mathf.Max(source.size.x, source.size.z);
        float targetLength = Mathf.Max(targetBounds.size.x, targetBounds.size.z);
        if (sourceLength > 0.0001f)
            model.localScale = Vector3.one * Mathf.Clamp(targetLength / sourceLength, 0.005f, 100f);

        if (!TryGetLocalRendererBounds(referenceRoot, model.GetComponentsInChildren<Renderer>(true), out Bounds normalized)) return;
        Vector3 offset = targetBounds.center - normalized.center;
        offset.y = targetBounds.min.y - normalized.min.y;
        model.localPosition += offset;
    }

    public static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null) return;
        target.layer = layer;
        foreach (Transform child in target.transform) SetLayerRecursively(child.gameObject, layer);
    }
}
