// ExcelLoader.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Data;
using SimpleFileBrowser;
using ExcelDataReader;
using NPOI.SS.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;

public class ExcelLoader : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button openExcelButton;
    [SerializeField] private RectTransform gridContent;
    [SerializeField] private GameObject cellPrefab; // Prefab with a TextMeshProUGUI component

    private string currentFilePath;
    private IWorkbook workbook;
    private ISheet sheet;
    private DataTable currentTable;

    private void Awake()
    {
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

    private void LoadAndDisplayExcel(string filePath)
    {
        try
        {
            currentFilePath = filePath;
            // Read into DataTable (for display and ID lookup)
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

            // Open with NPOI for write support
            using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                workbook = WorkbookFactory.Create(fs);
            }
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
        foreach (Transform child in gridContent) Destroy(child.gameObject);

        int rows = table.Rows.Count;
        int[] desiredCols = { 0, 1, 6, 7 };
        int visibleCols = desiredCols.Length;

        var grid = gridContent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = visibleCols;
        }

        for (int r = 1; r < rows; r++)
        {
            foreach (int c in desiredCols)
            {
                if (c < 0 || c >= table.Columns.Count) continue;
                var cell = Instantiate(cellPrefab, gridContent);
                var text = cell.GetComponent<TMP_Text>();
                if (text != null)
                    text.text = table.Rows[r][c]?.ToString();
            }
        }

        Debug.Log($"Displayed {rows * visibleCols} cells from columns [{string.Join(",", desiredCols)}].");
    }

    /// <summary>
    /// Finds the zero-based row index in the DataTable matching the given ID in a specified column (default col 0).
    /// </summary>
    public int FindRowById(string id, int idColumnIndex = 0)
    {
        if (currentTable == null) return -1;
        for (int i = 1; i < currentTable.Rows.Count; i++)
        {
            var cellVal = currentTable.Rows[i][idColumnIndex]?.ToString();
            if (cellVal == id) return i;
        }
        return -1;
    }

    /// <summary>
    /// Updates a specific cell in the loaded workbook, saves, and refreshes the display.
    /// </summary>
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

        using (var fs = File.Open(currentFilePath, FileMode.Open, FileAccess.Write))
            workbook.Write(fs);

        PopulateGrid(currentTable);
        Debug.Log($"Cell updated at row {rowIndex + 1}, col {colIndex + 1} => {newValue}");
    }

    private void OnDestroy()
    {
        openExcelButton.onClick.RemoveListener(ShowFileDialog);
    }
}
