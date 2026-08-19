using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PassiveGoldPopupUI : MonoBehaviour
{
    [Header("Links")]
    public GoldManager goldManager;
    public RectTransform anchor;
    public GameObject popupPrefab;

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

    // Попапы, которые сейчас летят. Нужны только для того, чтобы вернуть их в пул,
    // если объект выключат посреди анимации.
    private readonly List<PooledObject> inFlight = new List<PooledObject>();

    private void OnDisable()
    {
        if (goldManager != null)
            goldManager.OnGoldGained -= OnGoldGained;

        // Этот компонент живёт внутри GoldFrame, а панель инвентаря прячет её целиком.
        // Деактивация объекта убивает корутины Animate, и Release() в их конце уже не
        // выполнится: попап навсегда завис бы на экране полупрозрачным и утёк из пула.
        // Возвращаем всё, что было в полёте, руками.
        StopAllCoroutines();

        for (int i = 0; i < inFlight.Count; i++)
        {
            if (inFlight[i] != null)
                inFlight[i].Release();
        }

        inFlight.Clear();
    }

    private void OnGoldGained(int amount, GoldSource source, Vector3? worldPos)
    {
        if (source != GoldSource.PassiveTick) return;
        if (amount <= 0) return;

        GameObject gold = PoolManager.Instance.Spawn(popupPrefab, anchor.parent, Quaternion.identity);

        PooledObject pooledObject = gold.GetComponent<PooledObject>();

        TMP_Text t = gold.GetComponent<TMP_Text>();
        t.text = $"+{amount}{suffix}";

        RectTransform rt = t.rectTransform;
        rt.anchoredPosition = anchor.anchoredPosition;

        inFlight.Add(pooledObject);
        StartCoroutine(Animate(rt, t, pooledObject));
    }

    private IEnumerator Animate(RectTransform rt, TMP_Text t, PooledObject pooledObject)
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

        inFlight.Remove(pooledObject);
        pooledObject.Release();
    }
}
