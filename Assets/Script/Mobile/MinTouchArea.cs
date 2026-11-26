using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Расширяет зону касания кнопки до 48 dp, не меняя её вид: на телефоне с плотным экраном
/// небольшая иконка остаётся иконкой, а промахнуться пальцем по ней труднее. Пересчитывается
/// при каждом появлении — масштаб канваса известен только у включённого экрана.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class MinTouchArea : MonoBehaviour
{
    // 48 dp — рекомендованный Android минимум для зоны касания; 1 dp = 1/160 дюйма.
    private const float MinTouchInches = 48f / 160f;

    private void OnEnable()
    {
        var graphic = GetComponent<Graphic>();
        var canvas = graphic.canvas != null ? graphic.canvas.rootCanvas : null;
        if (canvas == null || Screen.dpi <= 0f)
            return;

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float minUnits = Screen.dpi * MinTouchInches / scale;
        Vector2 size = graphic.rectTransform.rect.size;
        float padX = Mathf.Max(0f, (minUnits - size.x) * 0.5f);
        float padY = Mathf.Max(0f, (minUnits - size.y) * 0.5f);

        // Отрицательный отступ расширяет область попадания луча за границы картинки.
        graphic.raycastPadding = new Vector4(-padX, -padY, -padX, -padY);
    }
}
