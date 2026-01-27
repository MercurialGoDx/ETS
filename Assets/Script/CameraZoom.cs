using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [Header("Target (что зумим вокруг)")]
    public Transform pivot; // обычно башня/центр карты

    [Header("Zoom")]
    public float zoomSpeed = 8f;      // чувствительность колеса
    public float minDistance = 6f;    // насколько близко можно подлететь
    public float maxDistance = 25f;   // насколько далеко

    [Header("Smoothing")]
    public float smoothTime = 0.12f;  // 0.08–0.2 обычно идеально

    private float targetDistance;
    private float distanceVelocity;

    private void Start()
    {
        if (pivot == null)
        {
            Debug.LogError("[CameraZoomDistance] Pivot is not assigned.");
            enabled = false;
            return;
        }

        targetDistance = Vector3.Distance(transform.position, pivot.position);
    }

    private void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            targetDistance -= scroll * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        float currentDistance = Vector3.Distance(transform.position, pivot.position);
        float newDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, smoothTime);

        // Двигаемся по лучу от pivot к камере (сохраняем текущий угол)
        Vector3 dir = (transform.position - pivot.position).normalized;
        transform.position = pivot.position + dir * newDistance;
    }
}
