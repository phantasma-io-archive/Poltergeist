using Poltergeist;
using System.Linq;
using UnityEngine;

public class ComboBox
{
    private static bool forceToUnShow = false;
    private static int useControlID = -1;
    private bool isClickedComboButton = false;
    private int selectedItemIndex = 0;

    private string buttonStyle;
    private string boxStyle;

    public ComboBox() : this("button", "box")
    {
    }

    public ComboBox(string buttonStyle, string boxStyle)
    {
        this.buttonStyle = buttonStyle;
        this.boxStyle = boxStyle;
    }

    public int Show(Rect rect, string[] listContent, GUIStyle listStyle)
    {
        return Show(rect, listContent.Select(x => new GUIContent(x)).ToArray(), listStyle);
    }

    public int Show(Rect rect, GUIContent[] listContent, GUIStyle listStyle)
    {
        if (forceToUnShow)
        {
            forceToUnShow = false;
            isClickedComboButton = false;
        }

        bool done = false;
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        switch (Event.current.GetTypeForControl(controlID))
        {
            case EventType.MouseUp:
                {
                    if (isClickedComboButton)
                    {
                        done = true;
                    }
                }
                break;
        }

        var buttonContent = listContent[selectedItemIndex];
        if (GUI.Button(rect, buttonContent, buttonStyle))
        {
            if (useControlID == -1)
            {
                useControlID = controlID;
                isClickedComboButton = false;
            }

            if (useControlID != controlID)
            {
                forceToUnShow = true;
                useControlID = controlID;
            }
            isClickedComboButton = true;
        }

        if (isClickedComboButton)
        {
            Rect listRect = new Rect(rect.x, rect.y + WalletGUI.Units(2),
                      rect.width, WalletGUI.Units(3) * listContent.Length);

            GUI.Box(listRect, "");

            listRect = new Rect(rect.x, rect.y + WalletGUI.Units(2),
                      rect.width, WalletGUI.Units(2) * listContent.Length);

            int newSelectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent, 1, listStyle);
            if (newSelectedItemIndex != selectedItemIndex)
            {
                selectedItemIndex = newSelectedItemIndex;
                buttonContent = listContent[selectedItemIndex];
            }
        }

        if (done)
            isClickedComboButton = false;

        return selectedItemIndex;
    }

    public int SelectedItemIndex
    {
        get
        {
            return selectedItemIndex;
        }
        set
        {
            selectedItemIndex = value;
        }
    }
}