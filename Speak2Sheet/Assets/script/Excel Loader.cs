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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Security;
using System.Text;



public class ExcelLoader : MonoBehaviour
{

    [Header("Auto-Save")]
    [SerializeField] private AutoSaveController autoSaveController;

    [Header("Lookup Settings")]
    [Tooltip("Column index used when finding rows by Name")]
    [SerializeField] private int nameColumnIndex = 1;

    public int NameColumnIndex => nameColumnIndex;


    // A single cell change
    private struct Change
    {
        public int RowIndex;
        public int ColIndex;
        public string OldValue;
    }

    // Stack to hold your history
    private Stack<Change> undoStack = new Stack<Change>();

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

    [Header("Save Controls")]
    [SerializeField] private Button saveButton;   // assign in the Inspector
    [SerializeField] private Button undoButton;   // assign in the Inspector


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
    public void SetIdColumnIndex(int idx)    => idColumnIndex    = idx;
    public void SetNameColumnIndex(int idx)  => nameColumnIndex  = idx;
    public void SetGradeColumnIndex(int idx) => gradeColumnIndex = idx;

    private void Awake()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Excel Files", ".xls", ".xlsx"));
        FileBrowser.SetDefaultFilter(".xlsx");
        openExcelButton.onClick.AddListener(ShowFileDialog);
        saveButton.onClick.AddListener(OnSaveButtonClicked);
        // Existing code…
        if (undoButton != null)
        {
            undoButton.onClick.AddListener(UndoLastChange);
            undoButton.interactable = false;  // disabled until you have something to undo
        }
    }

    private void UndoLastChange()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("Nothing to undo.");
            return;
        }

        var change = undoStack.Pop();
        // revert the sheet
        var row = sheet.GetRow(change.RowIndex) ?? sheet.CreateRow(change.RowIndex);
        var cell = row.GetCell(change.ColIndex) ?? row.CreateCell(change.ColIndex);
        cell.SetCellValue(change.OldValue);

        // revert the DataTable
        if (currentTable != null
            && change.RowIndex < currentTable.Rows.Count
            && change.ColIndex < currentTable.Columns.Count)
        {
            currentTable.Rows[change.RowIndex][change.ColIndex] = change.OldValue;
        }

        PopulateGrid(currentTable);
        Debug.Log($"Undid change at row {change.RowIndex + 1}, col {change.ColIndex + 1}: restored '{change.OldValue}'");

        // disable if we're out of history
        undoButton.interactable = (undoStack.Count > 0);
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

    private void OnSaveButtonClicked()
    {
        if (string.IsNullOrEmpty(currentFilePath) || workbook == null)
        {
            Debug.LogError("No file loaded to save!");
            return;
        }

        try
        {
            using (var fs = new FileStream(currentFilePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fs);
            }
            Debug.Log("Excel file saved to disk.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save Excel file: {ex.Message}");
        }
    }

    public void SaveFile()
    {
        OnSaveButtonClicked();
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

    /// <summary>
    /// Re-applies the current DesiredColumns & startRowIndex settings to the grid.
    /// </summary>
    public void RefreshDisplay()
    {
         Debug.Log($"[ExcelLoader] RefreshDisplay() called; DesiredColumns now = [{string.Join(",", desiredColumns)}]");
        if (currentTable != null)
            PopulateGrid(currentTable);
    }

    private void PopulateGrid(DataTable table)
    {
        Debug.Log($"[ExcelLoader] PopulateGrid: startRow={startRowIndex}, totalRows={table.Rows.Count}, visibleCols={desiredColumns.Count}");
        foreach (Transform child in gridContent) Destroy(child.gameObject);

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
                if (c < 0 || c >= table.Columns.Count) continue;
                var cell = Instantiate(cellPrefab, gridContent);
                var text = cell.GetComponent<TMP_Text>();
                if (text != null)
                    text.text = table.Rows[r][c]?.ToString();
            }
        }
    }

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

        // --- capture old value ---
        var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        var cell = row.GetCell(colIndex) ?? row.CreateCell(colIndex);
        string oldValue = cell.ToString();
        undoStack.Push(new Change
        {
            RowIndex = rowIndex,
            ColIndex = colIndex,
            OldValue = oldValue
        });

        // --- now apply the new one ---
        cell.SetCellValue(newValue);

        // also update your in-memory DataTable
        if (currentTable != null
            && rowIndex < currentTable.Rows.Count
            && colIndex < currentTable.Columns.Count)
        {
            currentTable.Rows[rowIndex][colIndex] = newValue;
        }

        PopulateGrid(currentTable);
        Debug.Log($"Cell updated in-memory at row {rowIndex + 1}, col {colIndex + 1} => {newValue}");
        autoSaveController.TriggerAutoSave(); // call your auto-save logic
                                              // enable Undo button now that we have history
        if (undoButton != null)
            undoButton.interactable = true;
    }


    private void OnDestroy()
    {
        openExcelButton.onClick.RemoveListener(ShowFileDialog);
    }

    /// <summary>
    /// Returns zero-based sheet row indices whose Column0 text contains or ends with partialId.
    /// </summary>
    // 1) Single-arg overload, uses the configured field:
public List<int> FindRowsByPartialId(string fragment)
    => FindRowsByPartialId(fragment, idColumnIndex);

// 2) The real implementation (no default!):
public List<int> FindRowsByPartialId(string partialId, int idColumnIndex)
{
    var matches = new List<int>();
    if (currentTable == null) return matches;

    for (int i = startRowIndex; i < currentTable.Rows.Count; i++)
    {
        var cell = currentTable.Rows[i][idColumnIndex]?.ToString() ?? "";
        if (cell.Contains(partialId) || cell.EndsWith(partialId, StringComparison.Ordinal))
            matches.Add(i);
    }
    return matches;
}


    /// <summary>
    /// Returns zero-based sheet row indices whose Column1 contains the given name fragment.
    /// </summary>
    /// <summary>
/// Fallback to use the configured nameColumnIndex.
/// </summary>
public List<int> FindRowsByPartialName(string fragment)
    => FindRowsByPartialName(fragment, nameColumnIndex);

/// <summary>
/// Full fuzzy‐search implementation, with an explicit column index.
/// </summary>
public List<int> FindRowsByPartialName(string nameFragment, int nameColumnIndex)
{
    var results = new List<int>();
    var fuzzy   = new List<(int row, int dist)>();
    if (currentTable == null) return results;

    // 1) Normalize the fragment
    string cleanFrag = CleanString(nameFragment);

    // 2) Dynamically pick a max distance:
    int dynamicMax = Math.Min(6, Math.Max(1, (int)Math.Ceiling(cleanFrag.Length * 0.5)));

    for (int i = startRowIndex; i < currentTable.Rows.Count; i++)
    {
        string raw  = currentTable.Rows[i][nameColumnIndex]?.ToString() ?? "";
        string cell = CleanString(raw);

        if (cell.Contains(cleanFrag))
            results.Add(i);
        else
        {
            int d = ComputeLevenshteinDistance(cell, cleanFrag);
            if (d <= dynamicMax) fuzzy.Add((i, d));
        }
    }

    // 3) Append fuzzy matches sorted by closeness
    foreach (var pair in fuzzy.OrderBy(x => x.dist))
        results.Add(pair.row);

    return results;
}


    private static string CleanString(string input)
    {
        // uppercase, strip punctuation, collapse spaces
        var s = Regex.Replace(input.ToUpperInvariant(), @"[^\p{L}\p{N}\s]+", "");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = new[] {
            d[i - 1, j] + 1,
            d[i, j - 1] + 1,
            d[i - 1, j - 1] + cost
        }.Min();
            }

        return d[n, m];
    }


    /// <summary>
    /// Helper to read any cell's current string.
    /// </summary>
    public string GetCellValue(int rowIndex, int colIndex)
    {
        if (currentTable == null
         || rowIndex < 1
         || rowIndex >= currentTable.Rows.Count
         || colIndex < 0
         || colIndex >= currentTable.Columns.Count)
            return "";
        return currentTable.Rows[rowIndex][colIndex]?.ToString() ?? "";
    }


}
