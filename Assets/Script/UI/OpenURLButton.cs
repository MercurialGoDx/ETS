using UnityEngine;

/// <summary>
/// Универсальная кнопка-ссылка: открывает URL в системном браузере.
/// Повесь на любую кнопку и укажи адрес в инспекторе.
/// </summary>
public class OpenURLButton : MonoBehaviour
{
    [Tooltip("Куда переходить по клику, напр. https://discord.gg/xxxxxxx")]
    [SerializeField] private string url;

    public void Open()
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning($"{name}: OpenURLButton.url не задан.");
            return;
        }

        Application.OpenURL(url);
    }
}
