using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Data;
using SimpleFileBrowser;
using ExcelDataReader;

public class ExcelLoader : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button      openExcelButton;
    [SerializeField] private RectTransform gridContent;
    [SerializeField] private GameObject  cellPrefab; // A prefab with a TextMeshProUGUI component

    private void Awake()
    {
        // Set up SimpleFileBrowser filters
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Excel Files", ".xls", ".xlsx"));
        FileBrowser.SetDefaultFilter(".xlsx");

        openExcelButton.onClick.AddListener(ShowFileDialog);
    }

    private void ShowFileDialog()
    {
        // Open file dialog with previously set filters
        FileBrowser.ShowLoadDialog(
            paths => LoadAndDisplayExcel(paths[0]),        // onSuccess
            ()      => Debug.Log("Excel load canceled"),  // onCancel
            FileBrowser.PickMode.Files,                     // pick mode
            false,                                          // multiselect disabled
            null,                                           // initial path
            null,                                           // initial filename
            "Select Excel File",                          // title
            "Open"                                        // load button text
        );
    }

    private void LoadAndDisplayExcel(string filePath)
    {
        try
        {
            // Encoding.RegisterProvider is not needed for .xlsx only support
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
                    PopulateGrid(dataSet.Tables[0]);
                else
                    Debug.LogWarning("No worksheets found in Excel file.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load Excel file: {ex.Message}");
        }
    }

    private void PopulateGrid(DataTable table)
    {
        // Clear existing cells
        foreach (Transform child in gridContent) Destroy(child.gameObject);

        int rows    = table.Rows.Count;
        int columns = table.Columns.Count;

        // Optionally adjust GridLayoutGroup on gridContent for these dimensions
        var grid = gridContent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }

        // Instantiate cells
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var cell = Instantiate(cellPrefab, gridContent);
                var text = cell.GetComponent<TMP_Text>();
                if (text != null)
                    text.text = table.Rows[r][c]?.ToString();
            }
        }

        Debug.Log($"Displayed {rows}x{columns} grid from Excel.");
    }
}
