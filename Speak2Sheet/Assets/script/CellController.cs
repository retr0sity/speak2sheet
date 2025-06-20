using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CellController : MonoBehaviour, IPointerClickHandler
{
    public TMP_Text       displayText;
    public TMP_InputField editField;

    private int rowIndex, colIndex;
    private float lastClickTime;
    private const float doubleClickThreshold = 0.3f;

    /// <summary>
    /// Called from PopulateGrid to set up this cell.
    /// </summary>
    public void Initialize(int row, int col, string value)
    {
        rowIndex   = row;
        colIndex   = col;
        displayText.text = value;
        editField.text   = value;
        editField.interactable = false;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (Time.unscaledTime - lastClickTime < doubleClickThreshold)
            BeginEdit();
        lastClickTime = Time.unscaledTime;
    }

    private void BeginEdit()
    {
        displayText.enabled    = false;
        editField.interactable = true;
        editField.ActivateInputField();
        editField.onEndEdit.AddListener(EndEdit);
    }

    private void EndEdit(string newValue)
    {
        editField.onEndEdit.RemoveListener(EndEdit);
        editField.interactable = false;
        displayText.enabled    = true;
        displayText.text       = newValue;

        // Push back to ExcelLoader
        var loader = UnityEngine.Object.FindFirstObjectByType<ExcelLoader>();
        loader.UpdateCell(rowIndex, colIndex, newValue);
    }
}
