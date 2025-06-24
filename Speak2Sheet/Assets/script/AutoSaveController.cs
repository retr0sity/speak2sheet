using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple controller to trigger auto-save of the Excel file.
/// Call TriggerAutoSave() from your own events (e.g. panel close, button click).
/// </summary>
public class AutoSaveController : MonoBehaviour
{
    [Tooltip("Toggle that enables/disables auto-save")]
    public Toggle autoSaveToggle;

    [Tooltip("Reference to your ExcelLoader component")]
    public ExcelLoader excelLoader;

    /// <summary>
    /// Call this method whenever you want to perform an auto-save check.
    /// </summary>
    public void TriggerAutoSave()
    {
        if (autoSaveToggle == null || excelLoader == null)
        {
            Debug.LogWarning("[AutoSaveController] Missing references: assign both Toggle and ExcelLoader.");
            return;
        }

        if (autoSaveToggle.isOn)
        {
            // Ensure your ExcelLoader exposes a public save method
            excelLoader.SaveFile();
            Debug.Log("[AutoSaveController] Auto-saved via ExcelLoader.SaveFile().");
        }
        else
        {
            Debug.Log("[AutoSaveController] Auto-save skipped (toggle is off).");
        }
    }
}

