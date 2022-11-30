using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Poltergeist
{
    public partial class WalletGUI : MonoBehaviour
    {
        private void DoButton(bool enabled, Rect rect, string text, Action callback)
        {
            var temp = GUI.enabled;
            GUI.enabled = enabled;
            if (GUI.Button(rect, text))
            {
                if (currentAnimation == AnimationDirection.None)
                {
                    callback();
                }
            }
            GUI.enabled = temp;
        }
        private void DoButton(bool enabled, bool pressed, Rect rect, string text, Action callback)
        {
            if (enabled && pressed)
            {
                callback();
            }
            else
            {
                var temp = GUI.enabled;
                GUI.enabled = enabled;
                if (GUI.Button(rect, text))
                {
                    if (currentAnimation == AnimationDirection.None)
                    {
                        callback();
                    }
                }
                GUI.enabled = temp;
            }
        }
        private void DoButton(bool enabled, Rect rect, Texture texture, bool active, Action callback)
        {
            var temp = GUI.enabled;
            GUI.enabled = enabled;
            if (active)
            {
                var style = new GUIStyle(GUI.skin.button);
                style.normal = style.hover;
                if (GUI.Button(rect, texture, style))
                {
                    if (currentAnimation == AnimationDirection.None)
                    {
                        callback();
                    }
                }
            }
            else
            {
                if (GUI.Button(rect, texture))
                {
                    if (currentAnimation == AnimationDirection.None)
                    {
                        callback();
                    }
                }
            }
            GUI.enabled = temp;
        }
        private Rect GetExpandedRect(int curY, int height)
        {
            var rect = new Rect(Border, curY, windowRect.width - Border * 2, height);
            return rect;
        }

        private void DoModalWindow(int windowID)
        {
            var accountManager = AccountManager.Instance;

            int curY = Units(4);

            var rect = new Rect(Units(1), curY, modalRect.width - Units(2), modalRect.height - Units(2));

            int captionHeight = Units(modalLineCount) + 4 * modalLineCount + Units(1) + 4;

            // Calculating, how much space caption can occupy vertically.
            // Substracting space for buttons: Units(8).
            int captionAvailableHeight = (int)rect.height - Units(8);

            if (modalState == ModalState.Input || modalState == ModalState.Password)
            {
                // Substracting space for input field: Units(2) * modalMaxLines + Units(2).
                captionAvailableHeight -= Units(2) * modalMaxLines + Units(2);
            }

            // Calculating, how much space caption will occupy vertically.
            int captionDisplayedHeight = Math.Min(captionAvailableHeight, captionHeight);

            int captionWidth = (int)rect.width;

            var insideRect = new Rect(0, 0, captionWidth, captionHeight);
            var outsideRect = new Rect(rect.x, curY, captionWidth, captionDisplayedHeight);

            bool needsScroll = insideRect.height > outsideRect.height;
            if (needsScroll)
            {
                captionWidth -= Border;
                insideRect.width = captionWidth;
            }

            modalCaptionScroll = GUI.BeginScrollView(outsideRect, modalCaptionScroll, insideRect);

            GUI.Label(insideRect, modalCaption);

            GUI.EndScrollView();

            if (_promptPicture != null)
            {
                GUI.DrawTexture(new Rect(16, curY - 32, 32, 32), _promptPicture, ScaleMode.ScaleToFit, true);
            }

            curY += Units(2);

            var fieldWidth = rect.width;

            bool hasHints = modalHints != null && modalHints.Count > 0;
            int hintWidth = Units(10);

            if (hasHints && !VerticalLayout)
            {
                fieldWidth -= hintWidth + Units(1);
            }

            curY += captionDisplayedHeight;

            int hintY;

            if (VerticalLayout)
            {
                hintY = curY + Units(2);
            }
            else
            {
                hintY = curY;
            }

            var enterPressed = false;
            var escapePressed = false;
            var e = UnityEngine.Event.current;
            if (e.type == EventType.KeyUp && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                enterPressed = true;
            }
            else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Escape)
            {
                escapePressed = true;
            }

            if (modalState == ModalState.Input)
            {
                if (modalMaxLines > 1)
                {
                    GUI.SetNextControlName("PoltergeistModalTextArea");
                    modalInput = GUI.TextArea(new Rect(rect.x, curY, fieldWidth, Units(2) * modalMaxLines), modalInput, modalMaxInputLength);
                    GUI.FocusControl("PoltergeistModalTextArea");
                }
                else
                {
                    GUI.SetNextControlName("PoltergeistModalTextField");
                    modalInput = GUI.TextField(new Rect(rect.x, curY, fieldWidth, Units(2)), modalInput, modalMaxInputLength);
                    GUI.FocusControl("PoltergeistModalTextField");
                }
            }
            else
            if (modalState == ModalState.Password)
            {
                GUI.SetNextControlName("PoltergeistModalPasswordField");
                modalInput = GUI.PasswordField(new Rect(rect.x, curY, fieldWidth, Units(2)), modalInput, '*', modalMaxInputLength);
                GUI.FocusControl("PoltergeistModalPasswordField");
            }

            int btnWidth = VerticalLayout ? ( (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString()) ? Units(9) : Units(7) + 8) : Units(11);

            curY = (int)(rect.height - Units(2));

            if (modalOptions.Length > 1)
            {
                int halfWidth = (int)(modalRect.width / 2);

                DoButton((!hasHints || !hintComboBox.DropDownIsOpened()),
                    escapePressed && (modalOptions == ModalConfirmCancel || modalOptions == ModalSendCancel || modalOptions == ModalYesNo),
                    new Rect((halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), modalOptions[1], () =>
                {
                    if (modalOptions == ModalOkCopy)
                    {
                        AudioManager.Instance.PlaySFX("click");
                        GUIUtility.systemCopyBuffer = modalCaption;
                    }
                    else if(modalOptions == ModalHexWif ||
                        modalOptions == ModalOkView)
                    {
                        AudioManager.Instance.PlaySFX("confirm");
                        modalResult = PromptResult.Failure;
                    }
                    else
                    {
                        AudioManager.Instance.PlaySFX("cancel");
                        modalResult = PromptResult.Failure;
                    }
                });

                DoButton((!hasHints || !hintComboBox.DropDownIsOpened()) && Time.time - modalTime >= modalConfirmDelay && ((modalState != ModalState.Input && modalState != ModalState.Password) || modalInput.Length >= modalMinInputLength),
                    enterPressed && (modalOptions == ModalOkCopy || modalOptions == ModalOkView || modalOptions == ModalConfirmCancel || modalOptions == ModalSendCancel || modalOptions == ModalYesNo),
                    new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), (modalConfirmDelay > 0 && (Time.time - modalTime < modalConfirmDelay)) ? modalOptions[0] + " (" + (modalConfirmDelay - Math.Floor(Time.time - modalTime)) + ")" : modalOptions[0], () =>
                {
                    AudioManager.Instance.PlaySFX("confirm");
                    modalResult = PromptResult.Success;
                });
            }
            else
            if (modalOptions.Length > 0)
            {
                DoButton(true,
                    (escapePressed || enterPressed) && modalOptions == ModalOk,
                    new Rect((modalRect.width - btnWidth) / 2, curY, btnWidth, Units(2)), modalOptions[0], () =>
                {
                    AudioManager.Instance.PlaySFX("click");
                    modalResult = PromptResult.Success;
                });
            }

            if (hasHints)
            {
                curY = hintY;

                int dropHeight;
                var hintList = modalHints.Keys.ToList();

                var prevHind = hintComboBox.SelectedItemIndex;
                var hintIndex = hintComboBox.Show(new Rect(rect.width - hintWidth + 8, curY, hintWidth, Units(2)), hintList, (int)modalRect.height - (curY + Units(2)) - Border, out dropHeight, modalHintsLabel);
                if (prevHind != hintIndex && hintIndex >= 0)
                {
                    var key = hintList[hintIndex];
                    if (modalHints.ContainsKey(key))
                    {
                        var temp = modalHints[key];
                        if (temp.StartsWith("|"))
                        {
                            temp = temp.Substring(1);
                            GUIState state;
                            
                            if (Enum.TryParse(temp, out state))
                            {
                                modalRedirected = true;
                                PushState(state);
                            }
                            else
                            {
                                MessageBox(MessageKind.Error, "Internal error decoding hint redirection.\nContact the developers.");
                            }
                        }
                        else
                        {
                            modalInput = temp;
                            modalInputKey = key;
                        }
                    }
                }
            }
        }

        // returns total items
        private int DoScrollArea<T>(ref Vector2 scroll, int startY, int endY, int panelHeight, IEnumerable<T> items, Action<T, int, int, Rect> callback)
        {
            int panelWidth = (int)(windowRect.width - (Border * 2));

            var itemCount = items != null ? items.Count() : 0;
            var insideRect = new Rect(0, 0, panelWidth, Border + ((panelHeight + Border) * itemCount));
            var outsideRect = new Rect(Border, startY, panelWidth, endY - (startY + Border));

            bool needsScroll = insideRect.height > outsideRect.height;
            if (needsScroll)
            {
                panelWidth -= Border;
                insideRect.width = panelWidth;
            }

            int curY = Border;

            int i = 0;
            scroll = GUI.BeginScrollView(outsideRect, scroll, insideRect);
            if (items != null)
            {
                foreach (var item in items)
                {
                    var rect = new Rect(0, curY, insideRect.width, panelHeight);
                    GUI.Box(rect, "");

                    callback(item, i, curY, rect);

                    curY += panelHeight;
                    curY += Border;
                    i++;
                }
            }
            GUI.EndScrollView();

            return i;
        }

        private void DoButtonGrid<T>(bool showBackground, int buttonCount, int xOffset, int yOffset, out int posY, Func<int, MenuEntry> options, Action<T> callback)
        {
            var border = Units(1);

            int panelHeight = VerticalLayout ? Border * 2 + (Units(2)+4) *  buttonCount : (border + Units(3));
            posY = (int)((windowRect.y + windowRect.height) - (panelHeight+ border)) + yOffset;

            var rect = new Rect(border, posY, windowRect.width - border * 2, panelHeight);
            
            if (showBackground)
            {
                GUI.Box(rect, "");
            }

            int divisionWidth = (int)((windowRect.width - xOffset * 2) / buttonCount);
            int btnWidth = (int)(divisionWidth * 0.8f);

            int maxBtnWidth = Units(8 + buttonCount * 2);
            if (btnWidth > maxBtnWidth)
            {
                btnWidth = maxBtnWidth;
            }

            int padding = (divisionWidth - btnWidth) / 2;

            T selected = default(T);
            bool hasSelection = false;

            for (int i = 0; i < buttonCount; i++)
            {
                var entry = options(i);

                Rect btnRect;

                if (VerticalLayout)
                {
                    btnRect = new Rect(rect.x + border*2, rect.y + border + i * (Units(2)+4), rect.width - border * 4, Units(2));
                }
                else
                {
                    btnRect = new Rect(divisionWidth * i + (divisionWidth - btnWidth) / 2 + xOffset, rect.y + border, btnWidth, Units(2));
                }

                DoButton(entry.enabled, btnRect, entry.label, () =>
                {
                    AudioManager.Instance.PlaySFX("click");
                    hasSelection = true;
                    selected = (T)entry.value;
                });
            }

            if (hasSelection)
            {
                callback(selected);
            }
        }

        // Methods for creating of NFT tools for toolbar over NFT list - used to create sort/filters combos, select/invert buttons etc.
        private int toolLabelWidth = Units(4) + 8;
        private int toolLabelHeight = Units(2);
        private int toolFieldWidth => (VerticalLayout) ? Units(7) : Units(9);
        private int toolFieldHeight = Units(1);
        private int toolFieldSpacing = Units(1);

        private void DoNftToolLabel(int posX, int posY, string label)
        {
            var style = GUI.skin.label;
            style.fontSize -= 6;
            if (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.black;
            GUI.Label(new Rect(posX, posY - 10, toolLabelWidth, toolLabelHeight), label);
            if (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.white;
            style.fontSize += 6;
        }

        private void DoNftToolTextField(int posX, int posY, string label, ref string result)
        {
            DoNftToolLabel(posX, posY, label);

            var style = GUI.skin.textField;
            style.fontSize -= 4;
            result = GUI.TextField(new Rect(posX + toolLabelWidth - 6, posY - 4, toolFieldWidth + 7, toolFieldHeight + 8), result);
            style.fontSize += 4;
        }

        private void DoNftToolComboBox<T>(int posX, int posY, ComboBox comboBox, IList<T> listContent, string label, ref int result)
        {
            DoNftToolLabel(posX, posY, label);

            comboBox.SelectedItemIndex = result;
            int dropHeight;
            result = comboBox.Show(new Rect(posX + toolLabelWidth, posY, toolFieldWidth, toolFieldHeight), listContent, 0, out dropHeight);
        }

        private void DoNftToolButton(int posX, int posY, int width, string label, Action callback)
        {
            var style = GUI.skin.button;
            style.fontSize -= 4;
            DoButton(true, new Rect(posX, posY, width, toolFieldHeight), label, callback);
            style.fontSize += 4;
        }

        private void DrawCenteredText(string caption)
        {
            var style = GUI.skin.label;
            var temp = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;

            if (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.black;
            GUI.Label(new Rect(0, 0, windowRect.width, windowRect.height), caption);
            if (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.white;

            style.alignment = temp;
        }

        private void DrawHorizontalCenteredText(int curY, float height, string caption)
        {
            var style = GUI.skin.label;
            var tempAlign = style.alignment;

            style.fontSize -= VerticalLayout ? 2: 4;
            style.alignment = TextAnchor.MiddleCenter;

            if(AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.black;
            GUI.Label(new Rect(0, curY, windowRect.width, height), caption);
            if (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.white;

            style.fontSize += VerticalLayout ? 2 : 4;
            style.alignment = tempAlign;
        }

        private void DoBackButton()
        {
            int posY;
            DoButtonGrid<bool>(false, 1, 0, Border, out posY, (index) =>
            {
                return new MenuEntry(true, "Back", true);
            }, (val) =>
            {
                PopState();
            });
        }
        private void DrawDropshadow(Rect rect)
        {
            float percent = 1/8f;
            var padX = rect.width * percent;
            var padY = rect.height * percent;
            var dropRect = new Rect(rect.x - padX, rect.y - padY, rect.width + padX * 2, rect.height + padY * 2);
            GUI.DrawTexture(dropRect, ResourceManager.Instance.Dropshadow);
        }
    }

}
