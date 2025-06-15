// ExcelLoader.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using SimpleFileBrowser;
using ExcelDataReader;
using NPOI.SS.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using System.Text;

public class ExcelLoader : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button openExcelButton;
    [SerializeField] private RectTransform gridContent;
    [SerializeField] private GameObject cellPrefab; // Prefab with a TextMeshProUGUI component

    [Header("Display Settings")]
    [Tooltip("Which column indices to show in the grid")]  
    [SerializeField] private List<int> desiredColumns = new List<int> { 0, 1, 6, 7 };
    [Tooltip("Start displaying from this row index (0-based, include header if needed)")]
    [SerializeField] private int startRowIndex = 1;

    [Header("Lookup Settings")]
    [Tooltip("Column index used when finding rows by ID")]  
    [SerializeField] private int idColumnIndex = 0;
    [Tooltip("Column index used for grades lookup")]  
    [SerializeField] private int gradeColumnIndex = 7;

    private string currentFilePath;
    private IWorkbook workbook;
    private ISheet sheet;
    private DataTable currentTable;

    public bool IsLoaded => sheet != null && currentTable != null;

    // Expose for other scripts or UI
    public List<int> DesiredColumns => desiredColumns;
    public int StartRowIndex => startRowIndex;
    public int IdColumnIndex => idColumnIndex;
    public int GradeColumnIndex => gradeColumnIndex;

    private void Awake()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Excel Files", ".xls", ".xlsx"));
        FileBrowser.SetDefaultFilter(".xlsx");
        openExcelButton.onClick.AddListener(ShowFileDialog);
    }

    private void ShowFileDialog()
    {
        FileBrowser.ShowLoadDialog(
            paths => LoadAndDisplayExcel(paths[0]),
            () => Debug.Log("Excel load canceled"),
            FileBrowser.PickMode.Files,
            false,
            null,
            null,
            "Select Excel File",
            "Open"
        );
    }

    public void LoadAndDisplayExcel(string filePath)
    {
        try
        {
            currentFilePath = filePath;
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var config = new ExcelDataSetConfiguration
                {
                    UseColumnDataType = false,
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
                };
                var dataSet = reader.AsDataSet(config);
                if (dataSet.Tables.Count > 0)
                    currentTable = dataSet.Tables[0];
                else
                {
                    Debug.LogWarning("No worksheets found in Excel file.");
                    return;
                }
            }

            using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
                workbook = WorkbookFactory.Create(fs);
            sheet = workbook.GetSheetAt(0);

            PopulateGrid(currentTable);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load Excel file: {ex.Message}");
        }
    }

    private void PopulateGrid(DataTable table)
    {
        foreach (Transform child in gridContent)
            Destroy(child.gameObject);

        int rows = table.Rows.Count;
        int visibleCols = desiredColumns.Count;

        var grid = gridContent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = visibleCols;
        }

        for (int r = startRowIndex; r < rows; r++)
        {
            foreach (int c in desiredColumns)
            {
                if (c < 0 || c >= table.Columns.Count)
                    continue;
                var cell = Instantiate(cellPrefab, gridContent);
                var text = cell.GetComponent<TMP_Text>();
                if (text != null)
                    text.text = table.Rows[r][c]?.ToString();
            }
        }
    }

    /// <summary>
    /// Finds the row index whose ID (in the configured column) ends with the given string.
    /// </summary>
    public int FindRowById(string id)
    {
        if (currentTable == null)
            return -1;
        for (int i = startRowIndex; i < currentTable.Rows.Count; i++)
        {
            var cellValue = currentTable.Rows[i][idColumnIndex]?.ToString() ?? string.Empty;
            if (cellValue.EndsWith(id, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Retrieves the grade value for a given table row, using the configured grade column.
    /// </summary>
    public string GetGradeForRow(int rowIndex)
    {
        if (currentTable == null || rowIndex < startRowIndex || rowIndex >= currentTable.Rows.Count)
            return string.Empty;
        return currentTable.Rows[rowIndex][gradeColumnIndex]?.ToString() ?? string.Empty;
    }

    public void UpdateCell(int rowIndex, int colIndex, string newValue)
    {
        if (sheet == null || workbook == null || string.IsNullOrEmpty(currentFilePath))
        {
            Debug.LogError("No workbook loaded. Cannot update cell.");
            return;
        }

        var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        var cell = row.GetCell(colIndex) ?? row.CreateCell(colIndex);
        cell.SetCellValue(newValue);

        if (currentTable != null && rowIndex < currentTable.Rows.Count && colIndex < currentTable.Columns.Count)
            currentTable.Rows[rowIndex][colIndex] = newValue;

        using (var fs = new FileStream(currentFilePath, FileMode.Create, FileAccess.Write))
        {
            workbook.Write(fs);
        }

        PopulateGrid(currentTable);
        Debug.Log($"Cell updated at row {rowIndex + 1}, col {colIndex + 1} => {newValue}");
    }

    private void OnDestroy()
    {
        openExcelButton.onClick.RemoveListener(ShowFileDialog);
    }
}
