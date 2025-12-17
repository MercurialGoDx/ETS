using UnityEngine;
using UnityEngine.UI;

public class WaveTimerUI : MonoBehaviour
{
    public Image fillImage;   // сюда кинешь Image с типом Filled

    // 0..1
    public void SetProgress(float normalized)
    {
        if (fillImage == null) return;
        fillImage.fillAmount = Mathf.Clamp01(normalized);
    }
}
