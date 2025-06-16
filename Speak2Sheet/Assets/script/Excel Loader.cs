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

    public bool IsLoaded => sheet != null && currentTable != null;

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
    }

    public int FindRowById(string id, int idColumnIndex = 0)
    {
        if (currentTable == null) return -1;
        for (int i = 1; i < currentTable.Rows.Count; i++)
        {
            var cellValue = currentTable.Rows[i][idColumnIndex]?.ToString() ?? "";
            // match if the recorded ID ends with the spoken 4-digit id
            if (cellValue.EndsWith(id, StringComparison.Ordinal))
                return i;
        }
        return -1;
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

        // Option A: Truncate & overwrite
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

    /// <summary>
    /// Returns zero-based sheet row indices whose Column0 text contains or ends with partialId.
    /// </summary>
    public List<int> FindRowsByPartialId(string partialId, int idColumnIndex = 0)
    {
        var matches = new List<int>();
        if (currentTable == null) return matches;
        for (int i = 1; i < currentTable.Rows.Count; i++)
        {
            var cell = currentTable.Rows[i][idColumnIndex]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(cell) &&
                (cell.EndsWith(partialId, StringComparison.Ordinal)
                  || cell.Contains(partialId)))
            {
                matches.Add(i);
            }
        }
        return matches;
    }

    /// <summary>
    /// Returns zero-based sheet row indices whose Column1 contains the given name fragment.
    /// </summary>
    public List<int> FindRowsByPartialName(string nameFragment, int nameColumnIndex = 1)
{
    var results = new List<int>();
    var fuzzy   = new List<(int row, int dist)>();
    if (currentTable == null) return results;

    // 1) Normalize the fragment
    string cleanFrag = CleanString(nameFragment);

    // 2) Dynamically pick a max distance:
    //    - At least 1, up to 6
    //    - E.g. half the fragment length, capped at 6
    int dynamicMax = Math.Min(6, Math.Max(1, (int)Math.Ceiling(cleanFrag.Length * 0.5)));

    for (int i = 1; i < currentTable.Rows.Count; i++)
    {
        string raw  = currentTable.Rows[i][nameColumnIndex]?.ToString() ?? "";
        string cell = CleanString(raw);

        if (cell.Contains(cleanFrag))
        {
            // exact substring hit
            results.Add(i);
        }
        else
        {
            int d = ComputeLevenshteinDistance(cell, cleanFrag);
            if (d <= dynamicMax)
                fuzzy.Add((i, d));
        }
    }

    // 3) Append fuzzy matches in order of closeness
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
