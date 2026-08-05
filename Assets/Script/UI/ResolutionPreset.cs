using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResolutionPresets : MonoBehaviour
{
    [System.Serializable]
    public struct ResolutionPreset
    {
        public string label;
        public int width;
        public int height;
    }

    [Header("UI")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private bool applyOnChange = true;

    [Header("Presets (базовые)")]
    [SerializeField] private List<ResolutionPreset> presets = new List<ResolutionPreset>
    {
        new ResolutionPreset { label = "HD (1280×720)",         width = 1280, height = 720  },
        new ResolutionPreset { label = "Full HD (1920×1080)",   width = 1920, height = 1080 },
        new ResolutionPreset { label = "2K (2560×1440)",        width = 2560, height = 1440 },
        new ResolutionPreset { label = "4K (3840×2160)",        width = 3840, height = 2160 },
        // добавляй сюда свои варианты
        // new ResolutionPreset { label="1600×900", width=1600, height=900 },
    };

    private List<ResolutionPreset> availablePresets = new(); // только те, что поддерживаются монитором
    private int currentIndex = -1;                           // -1 = игрок ничего не выбирал

    // Разрешение храним как ширину/высоту, а не как индекс в availablePresets: список зависит от
    // монитора, и на другом экране тот же индекс указывал бы на другое разрешение.
    private const string PREF_RES_WIDTH  = "pref_res_width";
    private const string PREF_RES_HEIGHT = "pref_res_height";
    private const string PREF_FULLSCREEN = "pref_fullscreen";

    private void Start()
    {
        BuildAvailablePresets();
        BuildDropdown();
        LoadPrefsToUI();
        HookUI();
        Apply(); // применяем сохранённое (или ближайшее)
    }

    private void BuildAvailablePresets()
    {
        // Собираем поддерживаемые системой пары width/height
        var supported = new HashSet<(int w, int h)>();
        foreach (var r in Screen.resolutions)
            supported.Add((r.width, r.height));

        availablePresets = presets
            .Where(p => supported.Contains((p.width, p.height)))
            .ToList();

        // Если вдруг ничего не совпало (редко, но бывает) — хотя бы текущий экран добавим
        if (availablePresets.Count == 0)
        {
            availablePresets.Add(new ResolutionPreset
            {
                label = $"{Screen.width}×{Screen.height}",
                width = Screen.width,
                height = Screen.height
            });
        }
    }

    private void BuildDropdown()
    {
        if (resolutionDropdown == null) return;

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(availablePresets.Select(p => p.label).ToList());
    }

    private void LoadPrefsToUI()
    {
        // Есть сохранённый выбор игрока — берём его, иначе оставляем -1: это значит "ничего не
        // выбирали", и тогда Apply() не станет трогать разрешение, заданное системой.
        if (PlayerPrefs.HasKey(PREF_RES_WIDTH) && PlayerPrefs.HasKey(PREF_RES_HEIGHT))
        {
            int w = PlayerPrefs.GetInt(PREF_RES_WIDTH);
            int h = PlayerPrefs.GetInt(PREF_RES_HEIGHT);
            currentIndex = FindClosestAvailablePresetIndex(w, h);
        }

        if (resolutionDropdown != null)
        {
            // Для UI нужен какой-то валидный пункт, даже если игрок ещё ничего не выбирал —
            // показываем ближайший к текущему разрешению экрана.
            int shown = currentIndex >= 0 ? currentIndex : FindClosestAvailablePresetIndex(Screen.width, Screen.height);
            resolutionDropdown.value = Mathf.Clamp(shown, 0, availablePresets.Count - 1);
        }

        bool fs = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = fs;
    }

    private void HookUI()
    {
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(_ => { SavePrefs(); if (applyOnChange) Apply(); });

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(_ => { SavePrefs(); if (applyOnChange) Apply(); });
    }

    private void SavePrefs()
    {
        if (resolutionDropdown != null && availablePresets.Count > 0)
        {
            currentIndex = Mathf.Clamp(resolutionDropdown.value, 0, availablePresets.Count - 1);
            PlayerPrefs.SetInt(PREF_RES_WIDTH,  availablePresets[currentIndex].width);
            PlayerPrefs.SetInt(PREF_RES_HEIGHT, availablePresets[currentIndex].height);
        }

        if (fullscreenToggle != null)
            PlayerPrefs.SetInt(PREF_FULLSCREEN, fullscreenToggle.isOn ? 1 : 0);

        PlayerPrefs.Save();
    }

    private int FindClosestAvailablePresetIndex(int w, int h)
    {
        int bestIndex = 0;
        int bestScore = int.MaxValue;

        for (int i = 0; i < availablePresets.Count; i++)
        {
            int dw = Mathf.Abs(availablePresets[i].width - w);
            int dh = Mathf.Abs(availablePresets[i].height - h);
            int score = dw + dh;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // Если applyOnChange=false — повесь на кнопку Apply
    public void Apply()
    {
        if (availablePresets == null || availablePresets.Count == 0) return;

        // Пока игрок сам не выбрал разрешение, не трогаем то, что выставила система: по умолчанию
        // это нативное разрешение монитора. Раньше здесь при неподключённом дропдауне безусловно
        // брался индекс 0 — то есть самый первый пресет (1280x720) — и игра на любом экране
        // стартовала в 720p, а дальше её растягивал монитор.
        if (currentIndex < 0)
        {
            if (resolutionDropdown == null) return;
            currentIndex = Mathf.Clamp(resolutionDropdown.value, 0, availablePresets.Count - 1);
        }

        int idx = Mathf.Clamp(currentIndex, 0, availablePresets.Count - 1);
        bool fs = (fullscreenToggle != null) ? fullscreenToggle.isOn : Screen.fullScreen;

        var p = availablePresets[idx];
        Screen.SetResolution(p.width, p.height, fs);
    }
}
