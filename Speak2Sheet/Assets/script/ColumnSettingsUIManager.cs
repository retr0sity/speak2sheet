using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// UI manager for selecting Excel columns via A–Z dropdowns and toggles for displayed columns (A–I).
/// </summary>
public class ColumnSettingsUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Dropdown for the column index used for ID lookup (A=0, B=1, ...)")]
    public TMP_Dropdown idColumnDropdown;

    [Tooltip("Dropdown for the column index used for Name lookup (A=0, B=1, ...)")]
    public TMP_Dropdown nameColumnDropdown;

    [Tooltip("Dropdown for the column index used for Grade entry (A=0, B=1, ...)")]
    public TMP_Dropdown gradeColumnDropdown;

    [Header("Displayed Columns Toggles A–I")]
    [Tooltip("Toggle list for columns A (0) through I (8)")]
    public List<Toggle> columnToggles; // assign 9 toggles in Inspector

    [Header("References")]
    public ExcelLoader excelLoader;

    // Generated list of column labels A–Z
    private List<string> columnOptions;

    private void Awake()
    {
        columnOptions = Enumerable.Range(0, 26)
            .Select(i => ((char)('A' + i)).ToString())
            .ToList();
    }

    private void Start()
    {
        // Populate dropdown options
        idColumnDropdown.ClearOptions();
        nameColumnDropdown.ClearOptions();
        gradeColumnDropdown.ClearOptions();
        idColumnDropdown.AddOptions(columnOptions);
        nameColumnDropdown.AddOptions(columnOptions);
        gradeColumnDropdown.AddOptions(columnOptions);

        // Initialize selected index based on ExcelLoader settings
        idColumnDropdown.value = Mathf.Clamp(excelLoader.IdColumnIndex, 0, columnOptions.Count - 1);
        nameColumnDropdown.value = Mathf.Clamp(excelLoader.NameColumnIndex, 0, columnOptions.Count - 1);
        gradeColumnDropdown.value = Mathf.Clamp(excelLoader.GradeColumnIndex, 0, columnOptions.Count - 1);

        // Wire dropdown change events
        idColumnDropdown.onValueChanged.AddListener(OnIdColumnChanged);
        nameColumnDropdown.onValueChanged.AddListener(OnNameColumnChanged);
        gradeColumnDropdown.onValueChanged.AddListener(OnGradeColumnChanged);

        // Initialize toggles for columns A (index 0) through I (index 8)
        if (columnToggles == null || columnToggles.Count < 9)
            Debug.LogWarning("Please assign 9 Toggles for columns A–I in the inspector.");
        else
        {
            for (int i = 0; i < 9; i++)
            {
                int idx = i; // capture for listener
                var tog = columnToggles[idx];
                // Set toggle state based on current displayed columns
                tog.isOn = excelLoader.DesiredColumns.Contains(idx);
                Debug.Log($"[ColumnSettingsUI] Initial toggle {i} (col {columnOptions[i]}): {tog.isOn}");
                // Listen for changes
                tog.onValueChanged.AddListener(isOn => OnColumnToggleChanged(idx, isOn));
            }
        }
    }

    private void OnIdColumnChanged(int index)
    {
        excelLoader.SetIdColumnIndex(index);
    }

    private void OnNameColumnChanged(int index)
    {
        excelLoader.SetNameColumnIndex(index);
    }

    private void OnGradeColumnChanged(int index)
    {
        excelLoader.SetGradeColumnIndex(index);
    }

    private void OnColumnToggleChanged(int columnIndex, bool isOn)
    {
        Debug.Log($"[ColumnSettingsUI] Toggle for column {columnIndex} → {(isOn ? "ON" : "OFF")}");
        if (excelLoader == null)
        {
            Debug.LogWarning("[ColumnSettingsUIManager] ExcelLoader reference is missing.");
            return;
        }

        var cols = excelLoader.DesiredColumns;
        if (isOn)
        {
            if (!cols.Contains(columnIndex))
                cols.Add(columnIndex);
        }
        else
        {
            cols.Remove(columnIndex);
        }
        // ensure columns are in ascending order for consistent display
        cols.Sort();

        // Refresh the grid display
        excelLoader.RefreshDisplay();
    }
}
