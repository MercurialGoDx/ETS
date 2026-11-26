using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Окно «Введите ник для таблицы рекордов». Собирается в рантайме на собственном Canvas
/// поверх всего — сцену не трогаем (см. MobileHud). Оформление (шрифт и спрайт кнопок)
/// берётся из уже загруженных ресурсов игры.
///
/// Тексты здесь, а не в таблицах Localization: таблицы — сериализованные .asset, а окно
/// пока одно. При появлении ещё мобильных строк их стоит перенести в Game Labels.
/// </summary>
public class NicknamePrompt : MonoBehaviour
{
    private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
    {
        // заголовок, подсказка, ошибка, OK, позже
        { "ru", new[] { "Ник для таблицы рекордов", "Его увидят другие игроки", "От 3 до 16 символов: буквы, цифры, пробел, _ - .", "Готово", "Позже" } },
        { "en", new[] { "Nickname for the leaderboard", "Other players will see it", "3 to 16 characters: letters, digits, space, _ - .", "Done", "Later" } },
        { "de", new[] { "Name für die Bestenliste", "Andere Spieler sehen ihn", "3 bis 16 Zeichen: Buchstaben, Ziffern, Leerzeichen, _ - .", "Fertig", "Später" } },
        { "es", new[] { "Apodo para la clasificación", "Otros jugadores lo verán", "De 3 a 16 caracteres: letras, cifras, espacio, _ - .", "Listo", "Más tarde" } },
        { "fr", new[] { "Pseudo pour le classement", "Les autres joueurs le verront", "3 à 16 caractères : lettres, chiffres, espace, _ - .", "OK", "Plus tard" } },
        { "pt", new[] { "Apelido para o ranking", "Outros jogadores vão vê-lo", "De 3 a 16 caracteres: letras, números, espaço, _ - .", "Pronto", "Depois" } },
    };

    private static NicknamePrompt current;

    private Action<string> onClosed;
    private TMP_InputField input;
    private TMP_Text errorLabel;

    /// <summary>Показывает окно; onClosed получает проверенный ник или null, если игрок отложил ввод.</summary>
    public static void Show(Action<string> onClosed)
    {
        if (current != null)
            return;

        var go = new GameObject("NicknamePrompt");
        current = go.AddComponent<NicknamePrompt>();
        current.onClosed = onClosed;
        current.Build();
    }

    private static string[] LocalizedTexts()
    {
        string code = LocalizationSettings.SelectedLocale != null
            ? LocalizationSettings.SelectedLocale.Identifier.Code
            : "en";
        if (code.Length > 2)
            code = code.Substring(0, 2);
        return Texts.TryGetValue(code, out var texts) ? texts : Texts["en"];
    }

    private void Build()
    {
        string[] text = LocalizedTexts();
        TMP_FontAsset font = FindGameFont();
        Sprite buttonSprite = FindLoadedSprite("basic_button");

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        // Затемнение перехватывает касания, чтобы не нажать что-то под окном.
        var shade = CreateRect("Shade", transform);
        Stretch(shade);
        shade.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var panel = CreateRect("Panel", shade);
        panel.sizeDelta = new Vector2(900f, 470f);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.13f, 0.11f, 0.10f, 0.98f);

        CreateLabel(panel, text[0], font, 52f, new Vector2(0f, 170f), new Vector2(820f, 70f), Color.white);
        CreateLabel(panel, text[1], font, 32f, new Vector2(0f, 105f), new Vector2(820f, 50f), new Color(0.8f, 0.78f, 0.72f));

        input = CreateInput(panel, font);
        errorLabel = CreateLabel(panel, text[2], font, 26f, new Vector2(0f, -55f), new Vector2(820f, 40f), new Color(0.95f, 0.45f, 0.4f));
        errorLabel.gameObject.SetActive(false);

        CreateButton(panel, text[4], font, buttonSprite, new Vector2(-200f, -160f), OnLater);
        CreateButton(panel, text[3], font, buttonSprite, new Vector2(200f, -160f), OnConfirm);

        EnsureEventSystem();
        input.ActivateInputField();
    }

    private void OnConfirm()
    {
        if (!NicknameRules.TryNormalize(input.text, out string nickname))
        {
            errorLabel.gameObject.SetActive(true);
            return;
        }
        Close(nickname);
    }

    private void OnLater() => Close(null);

    private void Close(string nickname)
    {
        current = null;
        Destroy(gameObject);
        onClosed?.Invoke(nickname);
    }

    private void Update()
    {
        // «Назад» на Android — то же, что «Позже».
        if (Input.GetKeyDown(KeyCode.Escape))
            OnLater();
    }

    // ===================== ПОСТРОЕНИЕ =====================

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateLabel(RectTransform parent, string value, TMP_FontAsset font, float size,
        Vector2 position, Vector2 box, Color color)
    {
        var rect = CreateRect("Label", parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_InputField CreateInput(RectTransform parent, TMP_FontAsset font)
    {
        var rect = CreateRect("Input", parent);
        rect.anchoredPosition = new Vector2(0f, 15f);
        rect.sizeDelta = new Vector2(640f, 100f);
        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.95f, 0.92f, 0.85f);

        var area = CreateRect("Text Area", rect);
        Stretch(area);
        area.offsetMin = new Vector2(24f, 10f);
        area.offsetMax = new Vector2(-24f, -10f);
        area.gameObject.AddComponent<RectMask2D>();

        var textRect = CreateRect("Text", area);
        Stretch(textRect);
        var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = 48f;
        text.color = new Color(0.12f, 0.1f, 0.08f);
        text.alignment = TextAlignmentOptions.Center;

        var field = rect.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.targetGraphic = background;
        field.characterLimit = NicknameRules.MaxLength;
        field.contentType = TMP_InputField.ContentType.Standard;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.text = NicknameRules.Saved;
        return field;
    }

    private static void CreateButton(RectTransform parent, string label, TMP_FontAsset font, Sprite sprite,
        Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var rect = CreateRect("Button", parent);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(340f, 110f);
        var image = rect.gameObject.AddComponent<Image>();
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        else
        {
            image.color = new Color(0.25f, 0.22f, 0.2f);
        }

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var text = CreateLabel(rect, label, font, 42f, Vector2.zero, Vector2.zero, Color.white);
        Stretch((RectTransform)text.transform);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
            Debug.LogWarning("[NicknamePrompt] EventSystem не найден — окно не будет реагировать на касания.");
    }

    /// <summary>Шрифт любой видимой подписи игры, чтобы окно не выбивалось из стиля.</summary>
    private static TMP_FontAsset FindGameFont()
    {
        foreach (var label in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            if (label.font != null && label.isActiveAndEnabled)
                return label.font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static Sprite FindLoadedSprite(string spriteName)
    {
        foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite.name == spriteName)
                return sprite;
        }
        return null;
    }
}
