using System.Collections;
using UnityEngine;

public class EnemyAttackFeedback : MonoBehaviour
{
    [Header("Подпрыгивание")]
    public float jumpHeight = 0.5f;     
    public float jumpDuration = 0.1f;  

    [Header("Окрашивание")]
    public Color hitColor = Color.red;
    public float colorHoldTime = 0.5f;

    private Renderer rend;
    private Color originalColor;

    private Coroutine activeRoutine;

    private void Start()
    {
        // Берём рендер с модели врага (подходит для любых префабов)
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            originalColor = rend.material.color;
    }

    public void PlayFeedback()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(FeedbackRoutine());
    }

    private IEnumerator FeedbackRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 peakPos = startPos + Vector3.up * jumpHeight;

        if (rend != null)
            rend.material.color = hitColor;

        // Вверх
        float t = 0f;
        while (t < jumpDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, peakPos, t / jumpDuration);
            yield return null;
        }

        // Вниз
        t = 0f;
        while (t < jumpDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(peakPos, startPos, t / jumpDuration);
            yield return null;
        }

        // Держим цвет
        yield return new WaitForSeconds(colorHoldTime);

        if (rend != null)
            rend.material.color = originalColor;

        activeRoutine = null;
    }
}
