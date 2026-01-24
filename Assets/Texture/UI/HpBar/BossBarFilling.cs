using UnityEngine;

public class BossBarFilling : MonoBehaviour
{
    [SerializeField] private RectTransform fillContainer; // объект с RectMask2D
    private float maxWidth;

    void Awake()
    {
        // Запоминаем максимальную ширину (когда хп = 100%)
        maxWidth = fillContainer.rect.width;
    }

    // hpNormalized: 0..1
    public void SetHP(float hpNormalized)
    {
        hpNormalized = Mathf.Clamp01(hpNormalized);

        var size = fillContainer.sizeDelta;
        size.x = maxWidth * hpNormalized;
        fillContainer.sizeDelta = size;
    }
}
