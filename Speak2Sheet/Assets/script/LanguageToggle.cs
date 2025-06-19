using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class LanguageToggle : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Toggle for Greek ON / English OFF")]
    public Toggle greekToggle;

    private const string PREF_KEY = "SelectedLanguage";

    private void Awake()
    {

        Debug.Log($"Available locales: {LocalizationSettings.AvailableLocales.Locales.Count}");
foreach (var loc in LocalizationSettings.AvailableLocales.Locales)
    Debug.Log($" • {loc.Identifier.Code}");

        // 1. Load saved language preference (if any)
        string savedCode = PlayerPrefs.GetString(PREF_KEY, "en");
        SetLocaleImmediate(savedCode);

        // 2. Initialize toggle state without invoking its event
        bool isGreek = savedCode == "el";
        greekToggle.SetIsOnWithoutNotify(isGreek);

        // 3. Subscribe to user changes
        greekToggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDestroy()
    {
        greekToggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        string code = isOn ? "el" : "en";
        // Save preference
        PlayerPrefs.SetString(PREF_KEY, code);
        // Apply localization
        StartCoroutine(SetLocale(code));
    }

    // Immediately apply locale at startup (no wait)
    private void SetLocaleImmediate(string code)
    {
        var locale = LocalizationSettings.AvailableLocales
            .GetLocale(code);
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
    }

    // Coroutine to wait for initialization, then apply
    private IEnumerator SetLocale(string code)
    {
        yield return LocalizationSettings.InitializationOperation;
        var locale = LocalizationSettings.AvailableLocales
            .GetLocale(code);
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
    }
}