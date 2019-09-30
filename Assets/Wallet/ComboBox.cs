using Poltergeist;
using System.Collections.Generic;
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

    private static GUIStyle listStyle;

    public ComboBox() : this("button", "box")
    {
    }

    public ComboBox(string buttonStyle, string boxStyle)
    {
        this.buttonStyle = buttonStyle;
        this.boxStyle = boxStyle;
    }

    public int Show<T>(Rect rect, IList<T> listContent, out int height)
    {
        return Show(rect, listContent.Select(x => new GUIContent(x.ToString())).ToArray(), out height);
    }

    public int Show(Rect rect, IList<GUIContent> listContent, out int height)
    {
        if (listStyle == null)
        {
            listStyle = GUI.skin.customStyles[0];

            var normalTex = ResourceManager.TextureFromColor(new Color(0, 0, 0, 0.75f));
            var hoverTex = ResourceManager.TextureFromColor(Color.white);

            listStyle.normal.textColor = Color.white;

            listStyle.normal.background = normalTex;

            listStyle.onHover.background =
            listStyle.hover.background = hoverTex;

            listStyle.padding.left =
            listStyle.padding.right =
            listStyle.padding.top =
            listStyle.padding.bottom = WalletGUI.Units(1);
        }

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

        height = WalletGUI.Units(2);
        if (isClickedComboButton)
        {
            Rect listRect = new Rect(rect.x, rect.y + WalletGUI.Units(2),
                      rect.width, WalletGUI.Units(3) * listContent.Count);

            //  GUI.Box(listRect, "");

            height += WalletGUI.Units(2) * listContent.Count;
            listRect = new Rect(rect.x, rect.y + WalletGUI.Units(2), rect.width, height);

            int newSelectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent.ToArray(), 1, listStyle);
            if (newSelectedItemIndex != selectedItemIndex)
            {
                selectedItemIndex = newSelectedItemIndex;
                buttonContent = listContent[selectedItemIndex];
            }

            height += WalletGUI.Units(2);
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