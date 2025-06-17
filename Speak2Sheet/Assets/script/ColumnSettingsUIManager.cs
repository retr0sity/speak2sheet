using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// UI manager for selecting Excel columns via A–Z dropdowns.
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

    [Header("References")]
    [Tooltip("Reference to the ExcelLoader component")]
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
        idColumnDropdown.value    = Mathf.Clamp(excelLoader.IdColumnIndex,    0, columnOptions.Count - 1);
        nameColumnDropdown.value  = Mathf.Clamp(excelLoader.NameColumnIndex,  0, columnOptions.Count - 1);
        gradeColumnDropdown.value = Mathf.Clamp(excelLoader.GradeColumnIndex, 0, columnOptions.Count - 1);

        // Wire change events
        idColumnDropdown.onValueChanged.AddListener(OnIdColumnChanged);
        nameColumnDropdown.onValueChanged.AddListener(OnNameColumnChanged);
        gradeColumnDropdown.onValueChanged.AddListener(OnGradeColumnChanged);
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
}
