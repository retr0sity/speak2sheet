using UnityEngine;
using UnityEngine.UI;

public class ToggleFlag : MonoBehaviour
{
    [Header("Toggle & UI Elements")]
    public Toggle languageToggle;
    public Image flagImage;

    [Header("Flag Sprites")]
    public Sprite englishFlag;
    public Sprite greekFlag;

    private void Start()
    {
        // Add listener for value change
        languageToggle.onValueChanged.AddListener(UpdateFlag);

        // Set initial flag based on current state
        UpdateFlag(languageToggle.isOn);
    }

    private void UpdateFlag(bool isGreek)
    {
        flagImage.sprite = isGreek ? greekFlag : englishFlag;

        // Optional: Set the language here if you're using localization
        // LocalizationManager.Instance.SetLanguage(isGreek ? "el" : "en");
    }
}