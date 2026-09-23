using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    private Vector3 distance;
    private float shakeRemaining;
    private float shakeDuration;
    private float shakeIntensity;

    private void Start()
    {
        if (target != null)
        {
            distance = target.position - transform.position;
        }
    }

    private void LateUpdate()
    {
        if (target == null || target.position.y < -1f) return;
        Vector3 targetPosition = target.position - distance;

        Vector3 shakeOffset = Vector3.zero;
        if (shakeRemaining > 0f)
        {
            shakeRemaining -= Time.unscaledDeltaTime;
            float strength = Mathf.Clamp01(shakeRemaining / Mathf.Max(0.001f, shakeDuration));
            float sample = Time.unscaledTime * 24f;
            shakeOffset.x = (Mathf.PerlinNoise(sample, 0.17f) * 2f - 1f) * shakeIntensity * strength;
            shakeOffset.y = (Mathf.PerlinNoise(0.31f, sample) * 2f - 1f) * shakeIntensity * strength;
        }
        transform.position = targetPosition + shakeOffset;
    }

    public void Shake(float duration, float intensity)
    {
        shakeRemaining = Mathf.Max(shakeRemaining, duration);
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeIntensity = intensity;
    }

    public void ResetFollow()
    {
        shakeRemaining = 0f;
        shakeDuration = 0f;
        shakeIntensity = 0f;
        if (target != null) transform.position = target.position - distance;
    }
}
