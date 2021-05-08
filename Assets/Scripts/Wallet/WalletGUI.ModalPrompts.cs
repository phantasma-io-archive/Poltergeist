using Phantasma.SDK;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Poltergeist
{
    public partial class WalletGUI : MonoBehaviour
    {
        #region MODAL PROMPTS
        private string[] ModalNone = new string[] { };
        private string[] ModalOk = new string[] { "Ok" };
        private string[] ModalOkCopy = new string[] { "Ok", "Copy to clipboard" };
        private string[] ModalOkView = new string[] { "Ok", "View" };
        private string[] ModalConfirmCancel = new string[] { "Confirm", "Cancel" };
        private string[] ModalSendCancel = new string[] { "Send", "Cancel" };
        private string[] ModalYesNo = new string[] { "Yes" , "No" };
        private string[] ModalHexWif = new string[] { "HEX format", "WIF format" };
        private string[] ModalNeoEthereum = new string[] { "Neo", "Ethereum" };
        private string[] ModalCurrentLegacy = new string[] { "Current", "Legacy" };

        private string[] modalOptions;
        private int modalConfirmDelay;
        private bool modalRedirected;
        private float modalTime;
        private ModalState modalState;
        private Action<PromptResult, string> modalCallback;
        private string modalInput;
        private int modalMinInputLength;
        private int modalMaxInputLength;
        private string modalCaption;
        private Vector2 modalCaptionScroll;
        private string modalTitle;
        private int modalMaxLines;
        private string modalHintsLabel;
        private Dictionary<string, string> modalHints;
        private PromptResult modalResult;
        private int modalLineCount;

        private Texture2D _promptPicture;

        private void ShowModal(string title, string caption, ModalState state, int minInputLength, int maxInputLength, string[] options, int multiLine, Action<PromptResult, string> callback, int confirmDelay = 0, string defaultValue = "")
        {
            if (modalState == ModalState.None)
            {
                modalTime = Time.time;
            }

            modalResult = PromptResult.Waiting;
            modalInput = defaultValue;
            modalState = state;
            modalTitle = title;

            modalMinInputLength = minInputLength;
            modalMaxInputLength = maxInputLength;

            modalCaption = caption;
            modalCaptionScroll = Vector2.zero;
            modalCallback = callback;
            modalOptions = options;
            modalConfirmDelay = confirmDelay;
            modalHintsLabel = "...";
            modalHints = null;
            modalMaxLines = multiLine;
            hintComboBox.SelectedItemIndex = -1;
            hintComboBox.ListScroll = Vector2.zero;
            modalLineCount = 0;
            // Counting lines in label. Since labels are wrapped if they are longer than ~65 symbols (~30-40 for vertical layout),
            // we count longer labels too. But labels wrapping based not only on length,
            // but on content also, so we add 2x multiplier to be on a safe side.
            // TODO: Make a better algorithm capable of counting exact number of lines for label depending on label's width and font size.
            Array.ForEach(modalCaption.Split("\n".ToCharArray()), x => modalLineCount += (x.ToString().Length / ((VerticalLayout) ? 30 : 65)) * 2 + 1);
        }

        public void BeginWaitingModal(string caption)
        {
            ShowModal("Please wait...", caption, ModalState.Message, 0, 0, ModalNone, 1, (result, input) =>
            {
            });
        }

        public void EndWaitingModal()
        {
            if (modalOptions.Length == 0)
            {
                modalState = ModalState.None;
            }
        }

        public void PromptBox(string caption, string[] options, Action<PromptResult> callback, int confirmDelay = 0)
        {
            ShowModal("Confirmation", caption, ModalState.Message, 0, 0, options, 1, (result, input) =>
            {
                _promptPicture = null;
                callback(result);
            }, confirmDelay);
        }

        public void MessageBox(MessageKind kind, string caption, Action callback = null)
        {
            // try to have focus for Phantasma Link requests
            AppFocus.Instance.StartFocus();

            string title;
            string[] options;
            switch (kind)
            {
                case MessageKind.Success:
                    AudioManager.Instance.PlaySFX("positive");
                    title = "Success";
                    options = ModalOk;
                    break;

                case MessageKind.Error:
                    AudioManager.Instance.PlaySFX("negative");
                    title = "Error";
                    options = ModalOkCopy;
                    Log.Write($"Error MessageBox: {caption}");
                    break;

                default:
                    title = "Message";
                    options = ModalOk;
                    break;
            }

            ShowModal(title, caption, ModalState.Message, 0, 0, options, 1, (result, input) =>
            {
                callback?.Invoke();
            });
        }
        #endregion
    }

}
