using System.Collections;
using TMPro;
using UnityEngine;

public class PassiveGoldPopupUI : MonoBehaviour
{
    [Header("Links")]
    public GoldManager goldManager;
    public RectTransform anchor;
    public TMP_Text popupPrefab;

    [Header("Animation")]
    public float floatUp = 30f;
    public float duration = 0.8f;

    [Header("Style")]
    public string suffix = " золота";

    private void OnEnable()
    {
        if (goldManager != null)
            goldManager.OnGoldGained += OnGoldGained;
    }

    private void OnDisable()
    {
        if (goldManager != null)
            goldManager.OnGoldGained -= OnGoldGained;
    }

    private void OnGoldGained(int amount, GoldSource source, Vector3? worldPos)
    {
        if (source != GoldSource.PassiveTick) return;
        if (amount <= 0) return;

        TMP_Text t = Instantiate(popupPrefab, anchor.parent);
        t.text = $"+{amount}{suffix}";

        RectTransform rt = t.rectTransform;
        rt.anchoredPosition = anchor.anchoredPosition;

        StartCoroutine(Animate(rt, t));
    }

    private IEnumerator Animate(RectTransform rt, TMP_Text t)
    {
        Vector2 start = rt.anchoredPosition;
        Vector2 end = start + Vector2.up * floatUp;

        float time = 0f;
        Color c = t.color;

        while (time < duration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / duration);

            rt.anchoredPosition = Vector2.Lerp(start, end, k);
            c.a = 1f - k;
            t.color = c;

            yield return null;
        }

        Destroy(t.gameObject);
    }
}
