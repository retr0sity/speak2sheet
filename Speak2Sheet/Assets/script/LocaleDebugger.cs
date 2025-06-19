using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LocaleDebugger : MonoBehaviour
{
    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }
    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }
    void OnLocaleChanged(UnityEngine.Localization.Locale newLocale)
    {
        Debug.Log($"▶️ Locale changed to: {newLocale.Identifier.Code}");
    }
}
