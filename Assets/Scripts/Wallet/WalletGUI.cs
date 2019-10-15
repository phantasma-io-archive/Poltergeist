using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Phantasma.VM.Utils;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Contracts;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Domain;

using ZXing;
using ZXing.QrCode;
using System.Globalization;
using Phantasma.Core.Types;

namespace Poltergeist
{
    public enum MessageKind
    {
        Default,
        Error,
        Success
    }

    public struct MenuEntry
    {
        public readonly object value;
        public readonly string label;
        public readonly bool enabled;

        public MenuEntry(object value, string label, bool enabled)
        {
            this.value = value;
            this.label = label;
            this.enabled = enabled;
        }
    }

    public enum GUIState
    {
        Loading,
        Wallets,
        Balances,
        History,
        Account,
        Sending,
        Confirming,
        Settings,
        ScanQR,
        Backup,
        Exit,
        Fatal
    }

    public enum PromptResult
    {
        Waiting,
        Failure,
        Success,
        Canceled
    }

    public enum AnimationDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    public enum ModalState
    {
        None,
        Message,
        Input,
        Password,
    }

    public class WalletGUI : MonoBehaviour
    {
        public RawImage background;

        private Dictionary<PlatformKind, Texture2D> QRCodeTextures = new Dictionary<PlatformKind, Texture2D>();

        public const string WalletTitle = "Poltergeist Wallet";
        public const string MoneyFormat = "0.####";

        public int Border => Units(1);
        public int HalfBorder => Border/2;
        public const bool fullScreen = true;
        public bool VerticalLayout => virtualWidth < virtualHeight; //virtualWidth < 420;

        public GUISkin guiSkin;

        private Rect windowRect = new Rect(0, 0, 600, 400);
        private Rect defaultRect;

        private Rect modalRect;

        private GUIState guiState;
        private Stack<GUIState> stateStack = new Stack<GUIState>();

        private string transferSymbol;
        private Hash transactionHash;
        private bool needsConfirmation;
        private int confirmationCount;
        private DateTime lastTransactionConfirmation;

        private AnimationDirection currentAnimation;
        private float animationTime;
        private bool invertAnimation;
        private Action animationCallback;

        private bool HasAnimation => currentAnimation != AnimationDirection.None;

        private string currentTitle;

        private string seedPhrase;

        private int currencyIndex;
        private string[] currencyOptions;
        private ComboBox currencyComboBox = new ComboBox();

        private ComboBox platformComboBox = new ComboBox();

        private ComboBox hintComboBox = new ComboBox();

        private int nexusIndex;
        private ComboBox nexusComboBox = new ComboBox();

        private NexusKind[] availableNexus = Enum.GetValues(typeof(NexusKind)).Cast<NexusKind>().ToArray();

        private bool initialized;

        private int virtualWidth;
        private int virtualHeight;

        private string fatalError;

        public static int Units(int n)
        {
            return 16 * n;
        }

        void Start()
        {
            initialized = false;

            guiState = GUIState.Loading;

            Debug.Log(Screen.width + " x " + Screen.height);
            currencyOptions = AccountManager.Instance.Currencies.ToArray();
        }

        void OnEnable()
        {
            Application.logMessageReceived += LogCallback;
        }

        void LogCallback(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                fatalError = condition + "\nStack trace:\n" + stackTrace;
                SetState(GUIState.Fatal);
            }
        }

        void OnDisable()
        {
            Application.logMessageReceived -= LogCallback;
        }

        #region UTILS
        private void PushState(GUIState state)
        {
            if (guiState != GUIState.Loading)
            {
                stateStack.Push(guiState);
            }

            SetState(state);
        }

        private void SetState(GUIState state)
        {
            switch (guiState)
            {
                case GUIState.Backup:
                    seedPhrase = null;
                    break;

                case GUIState.ScanQR:
                    if (camTexture != null)
                    {
                        camTexture.Stop();
                        camTexture = null;
                    }
                    break;
            }

            if (state == GUIState.Exit)
            {
                CloseCurrentStack();
                return;
            }

            guiState = state;

            var accountManager = AccountManager.Instance;

            currentTitle = null;

            switch (state)
            {
                case GUIState.Fatal:
                    currentTitle = "Fatal Error";
                    break;

                case GUIState.Wallets:
                    currentTitle = "Wallet List";
                    accountScroll = Vector2.zero;

                    foreach (var tex in QRCodeTextures.Values)
                    {
                        Texture2D.Destroy(tex);
                    }

                    QRCodeTextures.Clear();
                    break;

                case GUIState.Balances:
                    currentTitle = "Balances for " + accountManager.CurrentAccount.name;
                    balanceScroll = Vector2.zero;
                    accountManager.RefreshBalances(false);
                    break;

                case GUIState.History:
                    currentTitle = "History for " + accountManager.CurrentAccount.name;
                    accountManager.RefreshHistory(false);
                    break;

                case GUIState.Account:
                    currentTitle = "Account details for " + accountManager.CurrentAccount.name;

                    if (QRCodeTextures.Count == 0)
                    {
                        var platforms = accountManager.CurrentAccount.platforms.Split();
                        foreach (var platform in platforms)
                        {
                            var address = accountManager.GetAddress(accountManager.CurrentIndex, platform);
                            var tex = GenerateQR($"{platform.ToString().ToLower()}://{address}");
                            QRCodeTextures[platform] = tex;
                        }
                    }
                    break;

                case GUIState.Settings:
                    {
                        currentTitle = accountManager.Settings.nexusKind != NexusKind.Unknown ? "Settings" : "Wallet Setup";
                        currencyComboBox.SelectedItemIndex = 0;
                        for (int i = 0; i < currencyOptions.Length; i++)
                        {
                            if (currencyOptions[i] == accountManager.Settings.currency)
                            {
                                currencyComboBox.SelectedItemIndex = i;
                                break;
                            }

                        }

                        nexusIndex = 0;
                        for (int i = 0; i < availableNexus.Length; i++)
                        {
                            if (availableNexus[i] == accountManager.Settings.nexusKind)
                            {
                                nexusIndex = i;
                                break;
                            }
                        }
                        nexusComboBox.SelectedItemIndex = nexusIndex;

                        break;
                    }

                case GUIState.Backup:
                    currentTitle = "Backup your seed phrase!";
                    break;

                case GUIState.ScanQR:
                    currentTitle = "QR scanning";
                    cameraError = false;
                    scanTime = Time.time;
                    break;
            }
        }

        private void PopState()
        {
            if (modalRedirected)
            {
                modalRedirected = false;
            }

            var state = stateStack.Pop();
            SetState(state);
        }

        public void Animate(AnimationDirection direction, bool invert, Action callback = null)
        {
            animationTime = Time.time;
            invertAnimation = invert;
            currentAnimation = direction;
            animationCallback = callback;
        }
        #endregion


        #region MODAL PROMPTS
        private string[] ModalNone = new string[] { };
        private string[] ModalOk = new string[] { "Ok" };
        private string[] ModalConfirmCancel = new string[] { "Confirm" , "Cancel"};
        private string[] ModalYesNo = new string[] { "Yes" , "No" };

        private string[] modalOptions;
        private bool modalRedirected;
        private float modalTime;
        private ModalState modalState;
        private Action<PromptResult, string> modalCallback;
        private string modalInput;
        private int modalMinInputLength;
        private int modalMaxInputLength;
        private string modalCaption;
        private string modalTitle;
        private int modalMaxLines;
        private Dictionary<string, string> modalHints;
        private PromptResult modalResult;
        private int modalLineCount;

        private void ShowModal(string title, string caption, ModalState state, int minInputLength, int maxInputLength, string[] options, int multiLine, Action<PromptResult, string> callback)
        {
            if (modalState == ModalState.None)
            {
                modalTime = Time.time;
            }

            modalResult = PromptResult.Waiting;
            modalInput = "";
            modalState = state;
            modalTitle = title;

            modalMinInputLength = minInputLength;
            modalMaxInputLength = maxInputLength;

            modalCaption = caption;
            modalCallback = callback;
            modalOptions = options;
            modalHints = null;
            modalMaxLines = multiLine;
            hintComboBox.SelectedItemIndex = -1;
            modalLineCount = 1 + modalCaption.Where(x => x == '\n').Count();
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

        public void PromptBox(string caption, string[] options, Action<PromptResult> callback)
        {
            ShowModal("Confirmation", caption, ModalState.Message, 0, 0, options, 1, (result, input) =>
            {
                callback(result);
            });
        }

        public void MessageBox(MessageKind kind, string caption, Action callback = null)
        {
            string title;
            switch (kind)
            {
                case MessageKind.Success:
                    AudioManager.Instance.PlaySFX("positive");
                    title = "Success";
                    break;

                case MessageKind.Error:
                    AudioManager.Instance.PlaySFX("negative");
                    title = "Error";
                    break;

                default:
                    title = "Message";
                    break;
            }

            ShowModal(title, caption, ModalState.Message, 0, 0, ModalOk, 1, (result, input) =>
            {
                callback?.Invoke();
            });
        }

        public void RequestPassword(string description, PlatformKind platforms, Action<PromptResult> callback)
        {
            var accountManager = AccountManager.Instance;

            if (!accountManager.HasSelection)
            {
                callback(PromptResult.Failure);
                return;
            }

            if (string.IsNullOrEmpty(accountManager.CurrentAccount.password) /*|| accountManager.Settings.nexusKind == NexusKind.Local_Net*/)
            {
                callback(PromptResult.Success);
                return;
            }

            AudioManager.Instance.PlaySFX("auth");
            ShowModal("Account Authorization", $"Account: {accountManager.CurrentAccount.name} ({platforms})\nAction: {description}\n\nInsert password to proceed...", ModalState.Password, MinPasswordLength, Account.MaxPasswordLength, ModalConfirmCancel, 1, (result, input) =>
            {
                var auth = result;

                if (auth == PromptResult.Success && input != accountManager.CurrentAccount.password)
                {
                    auth = PromptResult.Failure;
                }

                callback(auth);                
            });
        }
        #endregion

        private const int MaxResolution = 1024;

        private void Update()
        {
            /*if (Input.GetKeyDown(KeyCode.Z))
            {
                AccountState state = null;
                state.address += "";
            }*/

            if (Screen.width > Screen.height && Screen.width > MaxResolution)
            {
                virtualWidth = MaxResolution;
                virtualHeight = (int)((MaxResolution * Screen.height) / (float)Screen.width);
            }
            else
            if (Screen.height > MaxResolution)
            {
                virtualHeight = MaxResolution;
                virtualWidth = (int)((MaxResolution * Screen.width) / (float)Screen.height);
            }
            else
            {
                virtualWidth = Screen.width;
                virtualHeight = Screen.height;
            }

            if (this.guiState == GUIState.Loading && AccountManager.Instance.Ready && !HasAnimation)
            {
                Animate(AnimationDirection.Up, true, () =>
                {
                    AudioManager.Instance.PlaySFX("load");

                    stateStack.Clear();
                    PushState(GUIState.Wallets);

                    if (AccountManager.Instance.Settings.nexusKind == NexusKind.Unknown)
                    {
                        PushState(GUIState.Settings);
                    }

                    Animate(AnimationDirection.Down, false);
                });
            }

            if (initialized && currentAnimation != AnimationDirection.None)
            {
                float animationDuration = 0.5f;
                var delta = (Time.time - animationTime) / animationDuration;

                bool finished = false;
                if (delta >= 1)
                {
                    delta = 1;
                    finished = true;
                }

                if (invertAnimation)
                {
                    delta = 1 - delta;
                }

                windowRect.x = defaultRect.x;
                windowRect.y = defaultRect.y;

                switch (currentAnimation)
                {
                    case AnimationDirection.Left:
                        windowRect.x = Mathf.Lerp(-defaultRect.width, defaultRect.x, delta);
                        break;

                    case AnimationDirection.Right:
                        windowRect.x = Mathf.Lerp(virtualWidth + defaultRect.width, defaultRect.x, delta);
                        break;

                    case AnimationDirection.Up:
                        windowRect.y = Mathf.Lerp(-defaultRect.height, defaultRect.y, delta);
                        break;

                    case AnimationDirection.Down:
                        windowRect.y = Mathf.Lerp(virtualHeight + defaultRect.height, defaultRect.y, delta);
                        break;
                }

                if (finished)
                {
                    currentAnimation = AnimationDirection.None;

                    var temp = animationCallback;
                    animationCallback = null;
                    temp?.Invoke();
                }
            }
            else
            {
                if (!initialized)
                {
                    initialized = true;
                }

                if (fullScreen)
                {
                    windowRect.width = virtualWidth;
                    windowRect.height = virtualHeight;
                }
                else
                {
                    background.texture = null;
                    windowRect.width = Mathf.Min(800, virtualWidth) - Border * 2;
                    windowRect.height = Mathf.Min(800, virtualHeight) - Border * 2;
                }

                windowRect.x = (virtualWidth - windowRect.width) / 2;
                windowRect.y = (virtualHeight - windowRect.height) / 2;

                defaultRect = new Rect(windowRect);
            }

            if (modalResult != PromptResult.Waiting)
            {
                var temp = modalCallback;
                var result = modalResult;
                var success = modalResult == PromptResult.Success;
                modalState = ModalState.None;
                modalCallback = null;
                modalResult = PromptResult.Waiting;
                temp?.Invoke(result, success ? modalInput.Trim() : null);

                if (modalState == ModalState.None)
                {
                    modalTime = Time.time;
                }
            }
        }

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

        void OnGUI()
        {
            var scaleX = Screen.width / (float)virtualWidth;
            var scaleY = Screen.height / (float)virtualHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1.0f));

            GUI.skin = guiSkin;
            GUI.enabled = true;

            GUI.color = Color.white;

            var duration = 0.333f;
            var delta = (Time.time - modalTime) / duration;
            if (delta > 1.0f)
            {
                delta = 1;
            }

            bool hasModal = modalState != ModalState.None && !modalRedirected;

            if (!hasModal)
            {
                delta = 1 - delta;
            }

            var k = Mathf.Lerp(1, 0.4f, delta);
            GUI.color = new Color(1, 1, 1, k);

            if (guiState == GUIState.Loading)
            {
                if (!AccountManager.Instance.Ready)
                {
                    DrawCenteredText(AccountManager.Instance.Status);
                }
            }
            else
            {
                if (fullScreen)
                {
                    DoMainWindow(0);
                }
                else
                {
                    GUI.Window(0, windowRect, DoMainWindow, WalletTitle);
                }
            }

            GUI.color = Color.white;

            if (modalState != ModalState.None && !modalRedirected)
            {
                var modalWidth = Units(44);
                var modalHeight = Units(25 + modalLineCount);

                int maxModalWidth = virtualWidth - Border * 2;
                if (modalWidth > maxModalWidth)
                {
                    modalWidth = maxModalWidth;
                }

                int maxModalHeight = virtualHeight - Border * 2;
                if (modalHeight > maxModalHeight)
                {
                    modalHeight = maxModalHeight;
                }

                modalRect = new Rect((virtualWidth - modalWidth) / 2, (virtualHeight - modalHeight) / 2, modalWidth, modalHeight);
                modalRect = GUI.ModalWindow(0, modalRect, DoModalWindow, modalTitle);
            }
        }

        private Rect GetExpandedRect(int curY, int height)
        {
            var rect = new Rect(Border, curY, windowRect.width - Border * 2, height);
            return rect;
        }

        private void DoMainWindow(int windowID)
        {
            GUI.Box(new Rect(8, 8, windowRect.width - 16, Units(2)), WalletTitle);

            var style = GUI.skin.label;
            var tempSize = style.fontSize;
            style.fontSize = 14;
            GUI.Label(new Rect(windowRect.width / 2 + Units(7) - 4, 12, 32, Units(2)), Application.version);
            style.fontSize = tempSize;

            var accountManager = AccountManager.Instance;

            if (currentTitle != null && this.currentAnimation == AnimationDirection.None && !accountManager.Refreshing)
            {
                int curY = Units(3);

                var tempTitle = currentTitle;

                switch (guiState)
                {
                    case GUIState.Account:
                    case GUIState.Balances:
                    case GUIState.History:
                        var state = accountManager.CurrentState;
                        if (state != null)
                        {
                            if (VerticalLayout)
                            {
                                tempTitle = $"{tempTitle} [{state.name}]";
                            }
                            else
                            {
                                tempTitle = $"{tempTitle} [{state.name} @ {accountManager.CurrentPlatform}]";
                            }
                        }
                        break;
                }

                DrawHorizontalCenteredText(curY - 4, Units(2), tempTitle);
            }

            switch (guiState)
            {
                case GUIState.Sending:
                    DrawCenteredText("Sending transaction...");
                    break;

                case GUIState.Confirming:
                    DoConfirmingScreen();
                    break;

                case GUIState.Wallets:
                    DoWalletsScreen();
                    break;

                case GUIState.Settings:
                    DoSettingsScreen();
                    break;

                case GUIState.Balances:
                    DoBalanceScreen();
                    break;

                case GUIState.History:
                    DoHistoryScreen();
                    break;

                case GUIState.Account:
                    DoAccountScreen();
                    break;

                case GUIState.ScanQR:
                    DoScanQRScreen();
                    break;

                case GUIState.Backup:
                    DoBackupScreen();
                    break;

                case GUIState.Fatal:
                    DoFatalScreen();
                    break;
            }

            //GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }

        private void DoModalWindow(int windowID)
        {
            var accountManager = AccountManager.Instance;

            int curY = Units(4);

            var rect = new Rect(Units(1), curY, modalRect.width - Units(2), modalRect.height - Units(2));

            GUI.Label(new Rect(rect.x, curY, rect.width - Border, Units(3 * modalLineCount * 2)), modalCaption);
            curY += Units(2);

            var fieldWidth = rect.width;

            bool hasHints = modalHints != null && modalHints.Count > 0;
            int hintWidth = Units(10);

            if (hasHints && !VerticalLayout)
            {
                fieldWidth -= hintWidth + Units(1);
            }

            curY += Units(modalLineCount) + 4 * modalLineCount;

            int hintY;

            if (VerticalLayout)
            {
                curY += Units(3);
                hintY = curY + Units(2);
            }
            else
            {
                hintY = curY;
            }

            if (modalState == ModalState.Input)
            {
                if (modalMaxLines > 1)
                {
                    modalInput = GUI.TextArea(new Rect(rect.x, curY, fieldWidth, Units(2) * modalMaxLines), modalInput, modalMaxInputLength);
                }
                else
                {
                    modalInput = GUI.TextField(new Rect(rect.x, curY, fieldWidth, Units(2)), modalInput, modalMaxInputLength);
                }
            }
            else
            if (modalState == ModalState.Password)
            {
                modalInput = GUI.PasswordField(new Rect(rect.x, curY, fieldWidth, Units(2)), modalInput, '*', modalMaxInputLength);               
            }

            int btnWidth = VerticalLayout ? Units(7) : Units(11);

            curY = (int)(rect.height - Units(2));

            if (modalOptions.Length > 1)
            {
                int halfWidth = (int)(rect.width / 2);

                if (VerticalLayout)
                {
                    halfWidth += Units(1);
                }

                DoButton(true, new Rect((halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), modalOptions[1], () =>
                {
                    AudioManager.Instance.PlaySFX("cancel");
                    modalResult = PromptResult.Failure;
                });

                DoButton(modalState != ModalState.Input || modalInput.Length > 0,
                    new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), modalOptions[0], () =>
                {
                    AudioManager.Instance.PlaySFX("confirm");
                    modalResult = PromptResult.Success;
                });
            }
            else
            if (modalOptions.Length > 0)
            {
                DoButton(true, new Rect((rect.width - btnWidth) / 2, curY, btnWidth, Units(2)), modalOptions[0], () =>
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
                var hintIndex = hintComboBox.Show(new Rect(rect.width - hintWidth + 8, curY, hintWidth, Units(2)), hintList, out dropHeight, "...");
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
                        }
                    }
                }
            }
        }

        private void DoConfirmingScreen()
        {
            var accountManager = AccountManager.Instance;

            DrawCenteredText($"Confirming transaction {transactionHash}...");

            if (needsConfirmation)
            {
                var now = DateTime.UtcNow;
                var diff = now - lastTransactionConfirmation;
                if (diff.TotalSeconds >= 3)
                {
                    lastTransactionConfirmation = now;
                    needsConfirmation = false;
                    confirmationCount++;
                    accountManager.RequestConfirmation(transactionHash.ToString(), confirmationCount, (msg) =>
                    {
                        if (msg == null)
                        {
                            PopState();

                            accountManager.RefreshBalances(true, () =>
                            {
                                InvokeTransactionCallback(transactionHash);
                            });
                        }
                        else
                        if (msg.ToLower().Contains("pending"))
                        {
                            needsConfirmation = true;
                            lastTransactionConfirmation = DateTime.UtcNow;
                        }
                        else
                        {
                            PopState();
                            MessageBox(MessageKind.Error, msg, () =>
                            {
                                InvokeTransactionCallback(Hash.Null);
                            });
                        }
                    });
                }
            }
        }

        private void LoginIntoAccount(int index, Action<bool> callback = null)
        {
            var isNewAccount = !string.IsNullOrEmpty(seedPhrase);

            var accountManager = AccountManager.Instance;
            accountManager.SelectAccount(index);

            RequestPassword("Open wallet", accountManager.CurrentAccount.platforms, (auth) =>
            {
                if (auth == PromptResult.Success)
                {
                    if (isNewAccount)
                    {
                        accountManager.BlankState();
                    }
                    else
                    {
                        accountManager.RefreshTokenPrices();
                    }

                    Animate(AnimationDirection.Down, true, () => {
                        PushState(isNewAccount ? GUIState.Backup : GUIState.Balances);

                        Animate(AnimationDirection.Up, false, () =>
                        {
                            callback?.Invoke(true);
                        });
                    });
                }
                else
                if (auth == PromptResult.Failure)
                {
                    var account = accountManager.Accounts[index];
                    MessageBox(MessageKind.Error, $"Could not open '{account.name}' account.", () =>
                    {
                        callback?.Invoke(false);
                    });
                }
            });
        }

        private void ImportWallet(string wif, string password)
        {
            if (wif != null)
            {
                var accountManager = AccountManager.Instance;
                foreach (var account in accountManager.Accounts)
                {
                    if (account.WIF == wif)
                    {
                        MessageBox(MessageKind.Error, "This private key is already imported in a different account.");
                        return;
                    }
                }
            }

            ShowModal("Wallet Name", "Enter a name for your wallet", ModalState.Input, 3, 32, ModalConfirmCancel, 1, (result, name) =>
            {
                if (result == PromptResult.Success)
                {
                    if (password == null)
                    {
                        PromptBox("Do you want to add a password to this wallet?\nThe password will be required to open the wallet.\nIt will also be prompted every time you do a transaction", ModalYesNo, (wantsPass) =>
                        {
                            if (wantsPass == PromptResult.Success)
                            {
                                TrySettingWalletPassword(name, wif);
                            }
                            else
                            {
                                FinishCreateAccount(name, wif, "");
                            }
                        });
                    }
                    else
                    {
                        FinishCreateAccount(name, wif, password);
                    }
                }
            });
        }

        private string[] commonPasswords = new string[]
        {
            "password", "123456", "1234567", "12345678", "baseball", "football","letmein","monkey","696969",
            "abc123","mustang","michael","shadow","master","jennifer","111111","jordan","superman","fuckme","hunter",
            "fuckyou", "trustno1", "ranger","buster","thomas","robert","bitcoin","phantasma","wallet","crypto"
        };

        private const int MinPasswordLength = 6;

        private bool IsGoodPassword(string name, string password)
        {
            if (password == null || password.Length < MinPasswordLength)
            {
                return false;
            }

            if (password.ToLowerInvariant().Contains(name.ToLowerInvariant()))
            {
                return false;
            }

            foreach (var common in commonPasswords)
            {
                if (name.Equals(common, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private void TrySettingWalletPassword(string name, string wif)
        {
            ShowModal("Wallet Password", "Enter a password for your wallet", ModalState.Password, MinPasswordLength, 32, ModalConfirmCancel, 1, (passResult, password) =>
            {
                if (passResult == PromptResult.Success)
                {
                    if (IsGoodPassword(name, password))
                    {
                        FinishCreateAccount(name, wif, password);
                    }
                    else
                    {
                        MessageBox(MessageKind.Error, $"That password is either too short or too weak.\nNeeds at least {MinPasswordLength} characters and can't be easy to guess.", () =>
                        {
                            TrySettingWalletPassword(name, wif);
                        });
                    }
                }
                else
                {
                    FinishCreateAccount(name, wif, "");
                }
            });
        }

        private void FinishCreateAccount(string name, string wif, string password)
        {
            try
            {
                bool isNewWallet = wif == null;

                if (isNewWallet)
                {
                    var bip = new Bitcoin.BIP39.BIP39(128, "", Bitcoin.BIP39.BIP39.Language.English);
                    seedPhrase = bip.MnemonicSentence;
                    var privKey = bip.SeedBytes.Take(32).ToArray();
                    var keys = new PhantasmaKeys(privKey);
                    wif = keys.ToWIF();

#if UNITY_EDITOR
                    GUIUtility.systemCopyBuffer = seedPhrase;
#endif
                }

                var accountManager = AccountManager.Instance;
                int walletIndex = accountManager.AddWallet(name, PlatformKind.Phantasma | PlatformKind.Neo, wif, password);
                LoginIntoAccount(walletIndex, (succes) =>
                {
                    if (succes)
                    {
                        accountManager.SaveAccounts();
                    }
                    else
                    {
                        accountManager.DeleteAccount(walletIndex);
                    }
                });
            }
            catch (Exception e)
            {
                MessageBox(MessageKind.Error, "Error creating account.\n" + e.Message);
            }
        }

        private string[] accountOptions = new string[] { "Generate new wallet", "Import wallet", "Settings" };

        private Vector2 accountScroll;
        private Vector2 balanceScroll;

        private void DoWalletsScreen()
        {
            int endY;
            DoButtonGrid<int>(true, accountOptions.Length, 0, out endY, (index) =>
            {
                return new MenuEntry(index, accountOptions[index], true);
            },
            (selected) =>
            {
                switch (selected)
                {
                    case 0:
                        {
                            ImportWallet(null, null);
                            break;
                        }

                    case 1:
                        {
                            ShowModal("Wallet Import", "Supported inputs:\n12 word seed phrase\nPrivate key (HEX format)\nPrivate key (WIF format)\nEncrypted private key (NEP2)", ModalState.Input, 32, 1024, ModalConfirmCancel, 4, (result, key) =>
                            {
                                if (result == PromptResult.Success)
                                {
                                    if (key.Length == 52 && (key.StartsWith("K") || key.StartsWith("L")))
                                    {
                                        ImportWallet(key, null);
                                    }
                                    else
                                    if (key.Length == 64 || (key.Length == 66 && key.ToUpper().StartsWith("0X")))
                                    {
                                        var priv = Base16.Decode(key);
                                        var tempKey = new PhantasmaKeys(priv);
                                        ImportWallet(tempKey.ToWIF(), null);
                                    }
                                    else
                                    if (key.Length == 58 && key.StartsWith("6"))
                                    {
                                        ShowModal("NEP2 Encrypted Key", "Insert your wallet passphrase", ModalState.Password, 1, 64, ModalConfirmCancel, 1, (auth, passphrase) =>
                                        {
                                            if (auth == PromptResult.Success)
                                            {
                                                try
                                                {
                                                    var decryptedKeys = Phantasma.Neo.Core.NeoKeys.FromNEP2(key, passphrase);
                                                    ImportWallet(decryptedKeys.WIF, passphrase);
                                                }
                                                catch (Exception e)
                                                {
                                                    MessageBox(MessageKind.Error, "Could not import wallet.\n" + e.Message);
                                                }
                                            }
                                        });
                                    }
                                    else
                                    if (key.Count(x => x == ' ') == 11)
                                    {
                                        ShowModal("Seed import", "Enter seed password.\nIf you put a wrong password, the wrong public address will be generated.\nFor wallets created with v1.3 or later, it is ok to leave this blank.", ModalState.Input, 0, 64, ModalConfirmCancel, 1, (seedResult, password) =>
                                        {
                                            if (seedResult != PromptResult.Success)
                                            {
                                                return; // user canceled
                                            }

                                            ImportSeedPhrase(key, password);
                                        });
                                    }
                                    else
                                    {
                                        MessageBox(MessageKind.Error, "Invalid input format.");
                                    }
                                }
                            });
                            break;
                        }

                    case 2:
                        {
                            Animate(AnimationDirection.Up, true, () =>
                            {
                                PushState(GUIState.Settings);
                                Animate(AnimationDirection.Down, false);
                            });
                            break;
                        }
                }
            });

            var accountManager = AccountManager.Instance;

            int startY = (int)(windowRect.y + Units(5));

            int panelHeight = Units(6);

            DoScrollArea<Account>(ref accountScroll, startY, endY, panelHeight, accountManager.Accounts,
                (account, index, curY, rect) =>
                {
                    int btnWidth = Units(7);
                    int halfWidth = (int)(rect.width / 2);

                    Rect btnRect;

                    if (VerticalLayout)
                    {
                        GUI.Label(new Rect(Border * 2, curY , windowRect.width - Border * 2, Units(2)), account.name);
                        GUI.Label(new Rect(Border * 2, curY + Units(1), windowRect.width - Border * 2, Units(2)), $"[{account.platforms}]");
                        btnRect = new Rect((rect.width - btnWidth)/2, curY + Units(3) + 4, btnWidth, Units(2));
                    }
                    else
                    {
                        GUI.Label(new Rect(Border * 2, curY + Units(1), windowRect.width - Border * 2, Units(2)), account.ToString());
                        btnRect = new Rect(rect.width - (btnWidth + Units(2) + 4), curY + Units(2) - 4, btnWidth, Units(2));
                    }

                    DoButton(true, btnRect, "Open", () =>
                    {
                        LoginIntoAccount(index);
                    });
                });
        }


        private void ImportSeedPhrase(string key, string password)
        {
            try
            {
                var bip = new Bitcoin.BIP39.BIP39(key, password, Bitcoin.BIP39.BIP39.Language.English);
                var privKey = bip.SeedBytes.Take(32).ToArray();
                var decryptedKeys = new PhantasmaKeys(privKey);
                ImportWallet(decryptedKeys.ToWIF(), null);
            }
            catch (Exception e)
            {
                MessageBox(MessageKind.Error, "Could not import wallet.\n" + e.Message);
            }
        }

        // returns total items
        private int DoScrollArea<T>(ref Vector2 scroll, int startY, int endY, int panelHeight, IEnumerable<T> items, Action<T, int, int, Rect> callback)
        {
            int panelWidth = (int)(windowRect.width - (Border * 2));

            var itemCount = items.Count();
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
            foreach (var item in items)
            {
                var rect = new Rect(0, curY, insideRect.width, panelHeight);
                GUI.Box(rect, "");

                callback(item, i, curY, rect);

                curY += panelHeight;
                curY += Border;
                i++;
            }
            GUI.EndScrollView();

            return i;
        }

        private void DoButtonGrid<T>(bool showBackground, int buttonCount, int offset, out int posY, Func<int, MenuEntry> options, Action<T> callback)
        {
            var border = Units(1);

            int panelHeight = VerticalLayout ? Border * 2 + (Units(2)+4) *  buttonCount : (border + Units(3));
            posY = (int)((windowRect.y + windowRect.height) - (panelHeight+ border)) + offset;

            var rect = new Rect(border, posY, windowRect.width - border * 2, panelHeight);
            
            if (showBackground)
            {
                GUI.Box(rect, "");
            }

            int divisionWidth = (int)(rect.width / buttonCount);
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
                    btnRect = new Rect((Units(1) / 2) + 4 + padding + i * divisionWidth, rect.y + border, btnWidth, Units(2));
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

        private void DrawCenteredText(string caption)
        {
            var style = GUI.skin.label;
            var temp = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;

            GUI.contentColor = Color.black;
            GUI.Label(new Rect(0, 0, windowRect.width, windowRect.height), caption);
            GUI.contentColor = Color.white;

            style.alignment = temp;
        }

        private void DrawHorizontalCenteredText(int curY, float height, string caption)
        {
            var style = GUI.skin.label;
            var tempAlign = style.alignment;
            var tempSize = style.fontSize;

            style.fontSize = VerticalLayout ? 18: 0;
            style.alignment = TextAnchor.MiddleCenter;

            GUI.contentColor = Color.black;
            GUI.Label(new Rect(0, curY, windowRect.width, height), caption);
            GUI.contentColor = Color.white;

            style.fontSize = tempSize;
            style.alignment = tempAlign;
        }

        private void CloseCurrentStack()
        {
            Animate(AnimationDirection.Down, true, () =>
            {
                var accountManager = AccountManager.Instance;
                accountManager.UnselectAcount();
                stateStack.Clear();
                PushState(GUIState.Wallets);

                Animate(AnimationDirection.Up, false);
            });
        }

        private bool ValidateSettings()
        {
            var accountManager = AccountManager.Instance;
            var settings = accountManager.Settings;

            if (settings.nexusKind == NexusKind.Unknown)
            {
                MessageBox(MessageKind.Error, "Select a Phantasma network first.");
                return false;
            }

            if (!settings.phantasmaRPCURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma RPC URL.\n" + settings.phantasmaRPCURL);
                return false;
            }

            if (!settings.neoRPCURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma RPC URL.\n" + settings.neoRPCURL);
                return false;
            }

            if (!settings.neoscanURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma RPC URL.\n" + settings.neoscanURL);
                return false;
            }

            if (settings.feePrice < 1)
            {
                MessageBox(MessageKind.Error, "Invalid value for fee price.\n" + settings.feePrice);
                return false;
            }

            if (accountManager.Accounts.Length == 0)
            {
                accountManager.InitDemoAccounts(settings.nexusKind);
            }

            accountManager.RefreshTokenPrices();
            accountManager.UpdateAPIs();
            accountManager.Settings.Save();
            return true;
        }

        private void DoSettingsScreen()
        {
            var accountManager = AccountManager.Instance;
            var settings = accountManager.Settings;

            int curY = Units(7);

            var fieldWidth = Units(20);

            int dropHeight;

            GUI.Box(new Rect(Border, curY - Border, windowRect.width - Border * 2, windowRect.height - (Border*2 + curY)), "");

            var posX = Units(3);

            GUI.Label(new Rect(posX, curY, Units(8), Units(2)), "Currency");
            currencyIndex = currencyComboBox.Show(new Rect(Units(11), curY, Units(8), Units(2)), currencyOptions, out dropHeight);
            settings.currency = currencyOptions[currencyIndex];
            curY += dropHeight + Units(1);

            settings.sfx = GUI.Toggle(new Rect(posX, curY, Units(2), Units(2)), settings.sfx, "");
            GUI.Label(new Rect(posX + Units(2), curY, Units(9), Units(2)), "Sound Effects");
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, Units(8), Units(2)), "Nexus");
            var nexusList = availableNexus.Select(x => x.ToString().Replace('_', ' ')).ToArray();
            var prevNexus = nexusIndex;
            nexusIndex = nexusComboBox.Show(new Rect(posX + Units(10), curY, Units(8), Units(2)), nexusList, out dropHeight, null, 1);
            settings.nexusKind = availableNexus[nexusIndex];
            curY += dropHeight + Units(1);

            if (settings.nexusKind != NexusKind.Main_Net && settings.nexusKind != NexusKind.Unknown)
            {
                var style = GUI.skin.label;
                var tempStyle = style.fontStyle;
                style.fontStyle = FontStyle.Italic;
                var warningHeight = Units(VerticalLayout ? 6: 2);
                GUI.Label(new Rect(posX, curY, windowRect.width - (posX +Border*2), warningHeight), "WARNING - Use this network only if you are a developer or tester.\nAll assets used here are only for development, not real.");
                style.fontStyle = tempStyle;
                curY += warningHeight + Units(1);
            }

            if (prevNexus != nexusIndex && settings.nexusKind != NexusKind.Custom)
            {
                settings.RestoreEndpoints(true);
            }

            bool hasCustomEndPoints;
            bool hasCustomFee;

            bool hasCustomName = settings.nexusKind == NexusKind.Custom;

            switch (settings.nexusKind)
            {
                case NexusKind.Custom:
                case NexusKind.Local_Net:
                    {
                        hasCustomEndPoints = true;
                        hasCustomFee = true;
                        break;
                    }

                default:
                    {
                        hasCustomEndPoints = false;
                        hasCustomFee = false;
                        hasCustomName = false;
                        break;
                    }
            }

            if (hasCustomEndPoints)
            {
                GUI.Label(new Rect(posX, curY, Units(8), Units(2)), "Phantasma RPC URL");
                settings.phantasmaRPCURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.phantasmaRPCURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, Units(8), Units(2)), "Neo RPC URL");
                settings.neoRPCURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.neoRPCURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, Units(8), Units(2)), "Neoscan API URL");
                settings.neoscanURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.neoscanURL);
                curY += Units(3);
            }
            else
            {
                settings.RestoreEndpoints(!hasCustomName);
            }

            if (hasCustomName)
            {
                GUI.Label(new Rect(posX, curY, Units(8), Units(2)), "Nexus Name");
                settings.nexusName = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.nexusName);
                curY += Units(3);
            }

            if (hasCustomFee)
            {
                GUI.Label(new Rect(posX, curY, Units(8), Units(2)), "Fee price");
                var fee = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.feePrice.ToString());
                BigInteger.TryParse(fee, out settings.feePrice);
                curY += Units(3);
            }

            if (accountManager.Accounts.Length > 0)
            {
                curY += Units(1);
                DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Delete Everything", () =>
                {
                    PromptBox("All wallets and settings stored in this device will be lost.\nMake sure you have backups of your private keys!\nOtherwise you will lose access to your funds.", ModalConfirmCancel, (result) =>
                    {
                        AudioManager.Instance.PlaySFX("click");
                        accountManager.DeleteAll();
                        PlayerPrefs.DeleteAll();
                        accountManager.Settings.Load();
                        MessageBox(MessageKind.Default, "All data removed from this device.", () =>
                        {
                            CloseCurrentStack();
                        });
                    });
                });

                curY += Units(3);
            }

            var btnWidth = Units(10);
            curY = (int)(windowRect.height - Units(6));
            DoButton(true, new Rect((windowRect.width - btnWidth) / 2, curY, btnWidth, Units(2)), "Confirm", () =>
            {
                if (ValidateSettings())
                {
                    AudioManager.Instance.PlaySFX("confirm");
                    CloseCurrentStack();
                }
            });
        }

        private int DrawPlatformTopMenu(Action refresh)
        {
            var accountManager = AccountManager.Instance;

            string mainToken;

            switch (accountManager.CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    mainToken = "SOUL";
                    break;

                case PlatformKind.Neo:
                    mainToken = "NEO";
                    break;

                default:
                    mainToken = null;
                    break;
            }

            int curY = VerticalLayout ? Units(6) : Units(1);

            if (mainToken != null)
            {
                GUI.DrawTexture(new Rect(Units(2), curY - 4, 24, 24), ResourceManager.Instance.GetToken(mainToken));
            }

            int currentPlatformIndex = 0;
            var platformList = accountManager.CurrentAccount.platforms.Split();

            if (platformList.Count > 1)
            {
                for (int i = 0; i < platformList.Count; i++)
                {
                    if (platformList[i] == accountManager.CurrentPlatform)
                    {
                        currentPlatformIndex = i;
                        break;
                    }
                }
                platformComboBox.SelectedItemIndex = currentPlatformIndex;

                int dropHeight;
                var platformIndex = platformComboBox.Show(new Rect(Units(4) + 8, curY, Units(8), Units(1)), platformList, out dropHeight);

                if (platformIndex != currentPlatformIndex)
                {
                    accountManager.CurrentPlatform = platformList[platformIndex];
                }
            }

            var state = accountManager.CurrentState;
            if (state == null)
            {
                return curY;
            }

            var btnWidth = Units(8);

            int refreshOffset = 2;

            if (VerticalLayout)
            {
                refreshOffset += 3;
            }

            if (refresh != null)
            {
                DoButton(true, new Rect(windowRect.width - (btnWidth + Border * refreshOffset), curY, btnWidth, Units(1)), "Refresh", () =>
                {
                    refresh();
                });
            }

            curY += Units(VerticalLayout ? 2 : 3);
            DrawHorizontalCenteredText(curY - 5, Units(VerticalLayout ? 3: 2), state.address);

            curY += Units(3);

            DoButton(true, new Rect((windowRect.width - btnWidth) / 2, curY, btnWidth, Units(1)), "Copy Address", () =>
              {
                  AudioManager.Instance.PlaySFX("click");
                  GUIUtility.systemCopyBuffer = state.address;
                  MessageBox(MessageKind.Default, "Address copied to clipboard.");
              });

            curY += Units(3);

            return curY;
        }

        private void DrawBalanceLine(ref Rect subRect, string symbol, decimal amount, string caption)
        {
            if (amount > 0.0001m)
            {
                var style = GUI.skin.label;
                var tempSize = style.fontSize;
                var tempColor = style.normal.textColor;
                style.normal.textColor = new Color(1, 1, 1, 0.75f);
                style.fontSize = VerticalLayout ? 16: 18;

                GUI.Label(subRect, $"{amount.ToString(MoneyFormat)} {symbol} {caption} ({AccountManager.Instance.GetTokenWorth(symbol, amount)})");
                style.fontSize = tempSize;
                style.normal.textColor = tempColor;

                subRect.y += 12;
            }
        }

        private WebCamTexture camTexture;
        private bool cameraError;
        private float scanTime;

        private void DoScanQRScreen()
        {
            var accountManager = AccountManager.Instance;

            if (cameraError)
            {
                DrawCenteredText("Failed to initialize camera...");
                return;
            }

            if (camTexture == null)
            {
                camTexture = new WebCamTexture();
                camTexture.requestedWidth = virtualWidth / 2;
                camTexture.requestedHeight = virtualHeight / 2;

                if (camTexture != null)
                {
                    camTexture.Play();
                }
                else
                {
                    cameraError = true;
                }
            }

            var camHeight = windowRect.height - Units(12);
            var camWidth  = (int)((camTexture.width * camHeight) / (float)camTexture.height);

            var camRect = new Rect((windowRect.width - camWidth)/2, Border + Units(5), camWidth, camHeight);
            DrawDropshadow(camRect);
            GUI.DrawTexture(camRect, camTexture, ScaleMode.ScaleToFit);

            var diff = Time.time - scanTime;
            if (diff >= 1 && camTexture != null && camTexture.isPlaying)
            {
                scanTime = Time.time;

                try
                {
                    IBarcodeReader barcodeReader = new BarcodeReader();
                    // decode the current frame
                    var result = barcodeReader.Decode(camTexture.GetPixels32(),
                      camTexture.width, camTexture.height);

                    if (result != null)
                    {
                        Debug.Log("DECODED TEXT FROM QR: " +result.Text);

                        foreach (var platform in AccountManager.AvailablePlatforms)
                        {
                            var tag = platform.ToString().ToLower()+"://";
                            if (result.Text.StartsWith(tag))
                            {
                                AudioManager.Instance.PlaySFX("positive");
                                modalInput = result.Text.Substring(tag.Length);
                                PopState();
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.LogWarning(ex.Message); }
            }

            DoBackButton();
        }

        private void DoBackupScreen()
        {
            int curY;

            curY = Units(5);
            GUI.Label(new Rect(Border, curY, windowRect.width - Border * 2, Units(6)), seedPhrase);

            curY += Units(11);
            int warningHeight = Units(16);
            int padding = 4;
            var rect = new Rect(padding, curY, windowRect.width - padding * 2, warningHeight);

            GUI.Box(rect, "");

            rect.x += Border;
            rect.y += 4;
            rect.width -= Border * 3;

            GUI.Label(rect, "WARNING");
            rect.y += Border*2;
            GUI.Label(rect, "For your own safety, write down these words on a piece of paper and store it safely and hidden.\n\nThe words serve as a back-up of your wallet.");

            var btnWidth = Units(10);
            curY = (int)(windowRect.height - Units(VerticalLayout ? 6: 7));
            DoButton(true, new Rect((windowRect.width - btnWidth) / 2, curY, btnWidth, Units(2)), "Continue", () =>
            {
                AudioManager.Instance.PlaySFX("confirm");
                PromptBox("You confirm that you have backup your private key?\nWithout a backup, it is impossible to recover your private key,\nand any funds in the account will be lost if something happens to this device.", ModalConfirmCancel, (result) =>
                {
                    if (result == PromptResult.Success)
                    {
                        SetState(GUIState.Account);
                    }
                });
            });
        }

        private void DoFatalScreen()
        {
            int curY;

            curY = Units(5);
            GUI.Label(new Rect(Border, curY, windowRect.width - Border * 2, windowRect.width - (Border+curY)), fatalError);

            var btnWidth = Units(12);
            curY = (int)(windowRect.height - Units(VerticalLayout ? 6 : 7));
            DoButton(true, new Rect((windowRect.width - btnWidth) / 2, curY, btnWidth, Units(2)), "Copy to Clipboard", () =>
            {
                AudioManager.Instance.PlaySFX("confirm");
                GUIUtility.systemCopyBuffer = fatalError;
                MessageBox(MessageKind.Default, "Error log copied to clipboard.");
            });
        }

        private void DoBackButton()
        {
            int posY;
            DoButtonGrid<bool>(false, 1, Border, out posY, (index) =>
            {
                return new MenuEntry(true, "Back", true);
            }, (val) =>
            {
                PopState();
            });
        }

        private void DoBalanceScreen()
        {
            var accountManager = AccountManager.Instance;

            if (accountManager.Refreshing)
            {
                DrawCenteredText("Fetching balances...");
                return;
            }

            var startY = DrawPlatformTopMenu(() =>
            {
                accountManager.RefreshBalances(true);
            });
            var endY = DoBottomMenu();

            var state = accountManager.CurrentState;

            if (state == null)
            {
                DrawCenteredText("Temporary error, cannot display balances...");
                return;
            }

            if (state.flags.HasFlag(AccountFlags.Master) && ResourceManager.Instance.MasterLogo != null)
            {
                if (VerticalLayout)
                {
                    GUI.DrawTexture(new Rect(windowRect.width - Units(6), Units(4) - 8, Units(6), Units(6)), ResourceManager.Instance.MasterLogo);
                }
                else {
                    GUI.DrawTexture(new Rect(Units(1), Units(2) + 8, Units(8), Units(8)), ResourceManager.Instance.MasterLogo);
                }
            }

            int curY = Units(12);

            decimal feeBalance = state.GetAvailableAmount("KCAL");

            var balanceCount = DoScrollArea<Balance>(ref balanceScroll, startY, endY, VerticalLayout ? Units(6) : Units(5), state.balances.Where(x => x.Total >= 0.001m),
                DoBalanceEntry);

            if (balanceCount == 0)
            {
                DrawCenteredText($"No assets found in this {accountManager.CurrentPlatform} account.");
            }
        }

        private void DoBalanceEntry(Balance balance, int index, int curY, Rect rect)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            GUI.Box(rect, "");

            var icon = ResourceManager.Instance.GetToken(balance.Symbol);
            if (icon != null)
            {
                if (VerticalLayout)
                {
                    GUI.DrawTexture(new Rect(Units(1) + 4, curY + Units(4) - 4, Units(2), Units(2)), icon);
                }
                else
                {
                    GUI.DrawTexture(new Rect(Units(2), curY + Units(1), Units(2), Units(2)), icon);
                }
            }

            int btnWidth = Units(11);
            int halfWidth = (int)(rect.width / 2);

            var posY = curY + Units(1) - 8;

            int posX = VerticalLayout ? Units(1) : Units(5);

            var style = GUI.skin.label;
            var tempSize = style.fontSize;

            style.fontSize = VerticalLayout ? 20 : 0;
            GUI.Label(new Rect(posX, posY, rect.width - posX, Units(2)), $"{balance.Available.ToString(MoneyFormat)} {balance.Symbol} ({accountManager.GetTokenWorth(balance.Symbol, balance.Available)})");
            style.fontSize = tempSize;

            var subRect = new Rect(posX, posY + Units(1) + 4, Units(20), Units(2));
            DrawBalanceLine(ref subRect, balance.Symbol, balance.Staked, "staked");
            DrawBalanceLine(ref subRect, balance.Symbol, balance.Pending, "pending");
            DrawBalanceLine(ref subRect, balance.Symbol, balance.Claimable, "claimable");

            string secondaryAction = null;
            bool secondaryEnabled = false;
            Action secondaryCallback = null;

            string tertiaryAction = null;
            bool tertiaryEnabled = false;
            Action tertiaryCallback = null;

            if (balance.Pending > 0)
            {
                secondaryAction = "Claim";
                secondaryEnabled = true;
                secondaryCallback = () =>
                {
                    PromptBox($"You have {balance.Pending} {balance.Symbol} pending in your account.\nDo you want to claim it?", ModalYesNo, (result) =>
                    {
                        if (result == PromptResult.Success)
                        {
                            accountManager.SettleSwap(balance.PendingPlatform, accountManager.CurrentPlatform.ToString().ToLower(), balance.PendingHash, (settleHash) =>
                            {
                                if (settleHash != Hash.Null)
                                {
                                    ShowConfirmationScreen(settleHash, (hash) =>
                                    {
                                        if (hash != Hash.Null)
                                        {
                                            MessageBox(MessageKind.Success, $"Your {balance.Symbol} arrived in your {accountManager.CurrentPlatform} account.");
                                        }
                                    });
                                }
                                else
                                {
                                    MessageBox(MessageKind.Error, $"There was some error claiming your {balance.Symbol}...");
                                }
                            });
                        }
                    });
                };
            }
            else
                switch (balance.Symbol)
                {
                    case "SOUL":
                        if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                        {
                            secondaryAction = "Stake";
                            secondaryEnabled = balance.Available > 1.2m;
                            secondaryCallback = () =>
                            {
                                RequireAmount($"Stake SOUL", null, "SOUL", 0.1m, balance.Available, (selectedAmount) =>
                                {
                                    var expectedDailyKCAL = (selectedAmount + balance.Staked) * 0.002m;

                                    PromptBox($"Do you want to stake {selectedAmount} SOUL?\nYou will be able to claim {expectedDailyKCAL} KCAL per day.", ModalYesNo, (result) =>
                                    {
                                        if (result == PromptResult.Success)
                                        {
                                            RequestKCAL("SOUL", (kcal) =>
                                            {
                                                if (kcal == PromptResult.Success)
                                                {
                                                    var address = Address.FromText(state.address);

                                                    var sb = new ScriptBuilder();
                                                    var gasPrice = accountManager.Settings.feePrice;

                                                    sb.AllowGas(address, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                                    sb.CallContract("stake", "Stake", address, UnitConversion.ToBigInteger(selectedAmount, balance.Decimals));
                                                    sb.SpendGas(address);

                                                    var script = sb.EndScript();

                                                    SendTransaction($"Stake {selectedAmount} SOUL", script, "main", (hash) =>
                                                    {
                                                        if (hash != Hash.Null)
                                                        {
                                                            MessageBox(MessageKind.Success, "Your SOUL was staked!\nTransaction hash: " + hash);
                                                        }
                                                    });
                                                }
                                            });
                                        }
                                    });
                                });
                            };

                            if (balance.Staked > 0)
                            {
                                tertiaryAction = "Unstake";
                                tertiaryEnabled = (Timestamp.Now - state.stakeTime) >= 86400;
                                tertiaryCallback = () =>
                                {
                                    RequireAmount("Unstake SOUL", null, "SOUL", 0.1m, balance.Staked,
                                        (amount) =>
                                        {
                                            var line = amount == balance.Staked ? "You won't be able to claim KCAL anymore." : "The amount of KCAL that will be able to claim later will be reduced.";
                                            
                                            if (amount == balance.Staked  && accountManager.CurrentState.name != ValidationUtils.ANONYMOUS)
                                            {
                                                line += "\nYour account will also lose the current registed name.";
                                            }

                                            PromptBox($"Do you want to unstake {amount} SOUL?\n{line}", ModalYesNo, (result) =>
                                            {
                                                RequestKCAL("SOUL", (kcal) =>
                                                {
                                                    if (kcal == PromptResult.Success)
                                                    {
                                                        var address = Address.FromText(state.address);

                                                        var sb = new ScriptBuilder();
                                                        var gasPrice = accountManager.Settings.feePrice;

                                                        sb.AllowGas(address, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                                        sb.CallContract("stake", "Unstake", address, UnitConversion.ToBigInteger(amount, balance.Decimals));
                                                        sb.SpendGas(address);
                                                        var script = sb.EndScript();

                                                        SendTransaction($"Unstake {balance.Staked} SOUL", script, "main", (hash) =>
                                                        {
                                                            if (hash != Hash.Null)
                                                            {
                                                                MessageBox(MessageKind.Success, "Your SOUL was unstaked!\nTransaction hash: " + hash);
                                                            }
                                                        });
                                                    }
                                                });
                                            });
                                        });
                                };
                            }
                        }

                        break;

                    case "KCAL":
                        if (balance.Claimable > 0)
                        {
                            secondaryAction = "Claim";
                            secondaryEnabled = true;
                            secondaryCallback = () =>
                            {
                                PromptBox($"Do you want to claim KCAL?\nThere is {balance.Claimable} KCAL available.", ModalYesNo, (result) =>
                                {
                                    if (result == PromptResult.Success)
                                    {
                                        var address = Address.FromText(state.address);
                                        var gasPrice = accountManager.Settings.feePrice;

                                        var sb = new ScriptBuilder();

                                        if (balance.Available > 0)
                                        {
                                            sb.AllowGas(address, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                            sb.CallContract("stake", "Claim", address, address);
                                        }
                                        else
                                        {
                                            sb.CallContract("stake", "Claim", address, address);
                                            sb.AllowGas(address, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                        }

                                        sb.SpendGas(address);
                                        var script = sb.EndScript();

                                        SendTransaction($"Claim {balance.Claimable} KCAL", script, "main", (hash) =>
                                        {
                                            if (hash != Hash.Null)
                                            {
                                                MessageBox(MessageKind.Success, "You claimed some KCAL!\nTransaction hash: " + hash);
                                            }
                                        });
                                    }
                                });
                            };
                        }
                        else
                        if (balance.Available > 0)
                        {
                            secondaryAction = "Burn";
                            secondaryEnabled = true;
                            secondaryCallback = () =>
                            {
                                RequireAmount("Burning KCAL", null, "KCAL", 0.1m, balance.Available, (amount) =>
                                {
                                    var amountText = amount.ToString(MoneyFormat);
                                    PromptBox($"Do you want to burn {amountText} KCAL?\nIt will be sent to the SES energy bomb!", ModalYesNo, (result) =>
                                    {
                                        if (result == PromptResult.Success)
                                        {
                                            var address = Address.FromText(state.address);

                                            var burnAddress = Address.FromHash("bomb");

                                            var sb = new ScriptBuilder();
                                            var gasPrice = accountManager.Settings.feePrice;

                                            sb.AllowGas(address, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                            if (amount <= 0.1m)
                                            {
                                                sb.TransferBalance(balance.Symbol, address, burnAddress);                                                
                                            }
                                            else
                                            {
                                                sb.TransferTokens(balance.Symbol, address, burnAddress, UnitConversion.ToBigInteger(amount, balance.Decimals));
                                            }
                                            sb.SpendGas(address);
                                            var script = sb.EndScript();

                                            SendTransaction($"Burn {amountText} KCAL", script, "main", (hash) =>
                                            {
                                                if (hash != Hash.Null)
                                                {
                                                    MessageBox(MessageKind.Success, "Your burned some KCAL!\nTransaction hash: " + hash);
                                                }
                                            });
                                        }
                                    });
                                });
                            };
                        }
                        break;

                    case "GAS":
                        {
                            if (accountManager.CurrentPlatform == PlatformKind.Neo)
                            {
                                secondaryAction = "Claim";
                                secondaryEnabled = balance.Claimable > 0;
                                secondaryCallback = () =>
                                {
                                    MessageBox(MessageKind.Error, "Not supported yet...");
                                };
                            }
                            break;
                        }
                }

            int btnY = VerticalLayout ? Units(4): Units(1) + 8;

            if (!string.IsNullOrEmpty(tertiaryAction))
            {
                DoButton(tertiaryEnabled, new Rect(rect.x + rect.width - (Units(18) + 8), curY + btnY, Units(4) + 8, Units(2)), tertiaryAction, () =>
                {
                    AudioManager.Instance.PlaySFX("click");
                    tertiaryCallback?.Invoke();
                });
            }

            if (!string.IsNullOrEmpty(secondaryAction))
            {
                DoButton(secondaryEnabled, new Rect(rect.x + rect.width - (Units(12) + 8), curY + btnY, Units(4) + 8, Units(2)), secondaryAction, () =>
                {
                    AudioManager.Instance.PlaySFX("click");
                    secondaryCallback?.Invoke();
                });
            }

            DoButton(balance.Available > 0, new Rect(rect.x + rect.width - (Units(6) + 8), curY + btnY, Units(4) + 8, Units(2)), "Send", () =>
             {
                 AudioManager.Instance.PlaySFX("click");

                 transferSymbol = balance.Symbol;
                 var transferName = $"{transferSymbol} transfer";
                 Phantasma.SDK.Token transferToken;

                 accountManager.GetTokenBySymbol(transferSymbol, out transferToken);
                 
                 ShowModal(transferName, "Enter destination address", ModalState.Input, 3, 64, ModalConfirmCancel, 1, (result, destAddress) =>
                 {
                     if (result == PromptResult.Failure)
                     {
                         return; // user canceled
                    }

                     if (Address.IsValidAddress(destAddress))
                     {
                         if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                         {
                             ContinuePhantasmaTransfer(transferName, transferSymbol, destAddress);
                         }
                         else
                         {
                             ContinueSwap(PlatformKind.Phantasma, transferName, transferSymbol, destAddress);
                         }
                     }
                     else
                     if (Phantasma.Neo.Utils.NeoUtils.IsValidAddress(destAddress))
                     {
                         if (accountManager.CurrentPlatform == PlatformKind.Neo)
                         {
                             ContinueNeoTransfer(transferName, transferSymbol, destAddress);
                         }
                         else
                         if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                         {
                             ContinueSwap(PlatformKind.Neo, transferName, transferSymbol, destAddress);
                         }
                         else
                         {
                             MessageBox(MessageKind.Error, $"Direct transfers from {accountManager.CurrentPlatform} to this type of address not supported.");
                         }
                     }
                     else
                     if (ValidationUtils.IsValidIdentifier(destAddress) && destAddress != state.name)
                     {
                         BeginWaitingModal("Looking up account name");
                         accountManager.ValidateAccountName(destAddress, (lookupAddress) =>
                         {
                             EndWaitingModal();

                             if (lookupAddress != null)
                             {
                                 ContinuePhantasmaTransfer(transferName, transferSymbol, lookupAddress);
                             }
                             else
                             {
                                 MessageBox(MessageKind.Error, "No account with such name exists.");
                             }
                         });
                     }
                     else
                     {
                         MessageBox(MessageKind.Error, "Invalid destination address.");
                     }
                 });

                 GetAccountHints(accountManager.CurrentPlatform.GetTransferTargets(transferToken));
             });
        }

        private void DoHistoryScreen()
        {
            var accountManager = AccountManager.Instance;

            if (accountManager.Refreshing)
            {
                DrawCenteredText("Fetching history...");
                return;
            }

            var startY = DrawPlatformTopMenu(() =>
            {
                accountManager.RefreshHistory(true);
            });
            var endY = DoBottomMenu();

            var history = accountManager.CurrentHistory;

            if (history == null)
            {
                DrawCenteredText("Temporary error, cannot display history...");
                return;
            }

            int curY = Units(12);

            var historyCount = DoScrollArea<HistoryEntry>(ref balanceScroll, startY, endY, VerticalLayout ? Units(4) : Units(3), history,
                DoHistoryEntry);

            if (historyCount == 0)
            {
                DrawCenteredText($"No transactions found for this {accountManager.CurrentPlatform} account.");
            }

            DoBottomMenu();
        }

        private void DoHistoryEntry(HistoryEntry entry, int index, int curY, Rect rect)
        {
            int panelHeight = Units(3);
            int panelWidth = (int)(windowRect.width - Units(2));
            int padding = 8;

            int availableHeight = (int)(windowRect.height - (curY + Units(6)));
            int heightPerItem = panelHeight + padding;
            int maxEntries = availableHeight / heightPerItem;

            var accountManager = AccountManager.Instance;

            var date = String.Format("{0:g}", entry.date);

            int halfWidth = (int)(rect.width / 2);

            GUI.Label(new Rect(Units(2), curY + 4, Units(20), Units(2)), VerticalLayout ? entry.hash.Substring(0, 16)+"...": entry.hash);

            Rect btnRect;

            if (VerticalLayout)
            {
                curY += Units(2);
                GUI.Label(new Rect(Units(2), curY, Units(20), Units(2)), date);
                btnRect = new Rect(rect.x + rect.width - Units(6), curY + 4, Units(4), Units(1));
            }
            else
            {
                GUI.Label(new Rect(Units(26), curY + 4, Units(20), Units(2)), date);
                btnRect = new Rect(rect.x + rect.width - Units(6), curY + rect.height - (4 + Units(1)), Units(4), Units(1));
            }

            DoButton(!string.IsNullOrEmpty(entry.url), btnRect, "View", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                Application.OpenURL(entry.url);
            });

            curY += panelHeight + padding;
        }


        private void DoAccountScreen()
        {
            var accountManager = AccountManager.Instance;

            var startY = DrawPlatformTopMenu(null);
            var endY = DoBottomMenu();

            int curY = startY;

            curY = Units(10);

            if (VerticalLayout)
            {
                curY += Units(2) + 8;
            }

            int btnWidth = Units(8);
            int centerX = (int)(windowRect.width - btnWidth) / 2;

            var platform = accountManager.CurrentPlatform;
            if (QRCodeTextures.ContainsKey(platform))
            {
                var qrTex = QRCodeTextures[platform];
                var qrResolution = 200;
                var qrRect = new Rect((windowRect.width - qrResolution) / 2, curY, qrResolution, qrResolution);

                DrawDropshadow(qrRect);
                GUI.DrawTexture(qrRect, qrTex);
                curY += qrResolution;
                curY += Units(1);
            }

            curY = endY - Units(3);

            int btnOffset = Units(4);

            if (VerticalLayout)
            {
                btnOffset += Units(5)+8;
            }

            int posY;
            DoButtonGrid<int>(false, accountMenu.Length, -btnOffset, out posY, (index) =>
            {
                var enabled = true;

                if (accountManager.CurrentState != null)
                {
                    switch (index)
                    {
                        case 1:
                            if (accountManager.CurrentPlatform != PlatformKind.Phantasma || accountManager.CurrentState.name != "anonymous")
                            {
                                enabled = false;
                            }
                            break;

                        case 2:
                            if (accountManager.CurrentPlatform != PlatformKind.Phantasma || !accountManager.CurrentState.flags.HasFlag(AccountFlags.Validator))
                            {
                                enabled = false;
                            }
                            break;
                    }
                }
                else
                {
                    enabled = false;
                }

                return new MenuEntry(index, accountMenu[index], enabled);
            },
            (selected) =>
            {
                switch (selected)
                {
                    case 0:
                        {
                            AudioManager.Instance.PlaySFX("click");
                            GUIUtility.systemCopyBuffer = accountManager.CurrentAccount.WIF;
                            MessageBox(MessageKind.Default, "WIF copied to the clipboard.");
                            break;
                        }

                    case 1:
                        {
                            var state = accountManager.CurrentState;
                            decimal stake = state != null ? state.balances.Where(x => x.Symbol == DomainSettings.StakingTokenSymbol).Select(x => x.Staked).FirstOrDefault() : 0;

                            if (stake >= 1)
                            {
                                ShowModal("Setup Name", $"Enter a name for the chain address.\nOther users will be able to transfer assets directly to this name.", ModalState.Input, 3, 16, ModalConfirmCancel, 1, (result, name) =>
                                {
                                    if (result == PromptResult.Success)
                                    {
                                        if (ValidationUtils.IsValidIdentifier(name))
                                        {
                                            RequestKCAL(null, (kcalResult) =>
                                            {
                                                if (kcalResult == PromptResult.Success)
                                                {
                                                    byte[] script;

                                                    try
                                                    {
                                                        var gasPrice = accountManager.Settings.feePrice;

                                                        var source = Address.FromText(accountManager.CurrentState.address);

                                                        var sb = new ScriptBuilder();
                                                        sb.AllowGas(source, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                                        sb.CallContract("account", "RegisterName", source, name);
                                                        sb.SpendGas(source);
                                                        script = sb.EndScript();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message);
                                                        return;
                                                    }

                                                    SendTransaction($"Register address name\nName:{name}\nAddress:{accountManager.CurrentState.address}?", script, "main", (hash) =>
                                                    {
                                                        if (hash != Hash.Null)
                                                        {
                                                            SetState(guiState); // force updating the current UI

                                                            if (AccountManager.Instance.CurrentAccount.name != name)
                                                            {
                                                                PromptBox("The address name was set successfully.\nDo you also want to change the local name for the account?\nThe local name is only visible in this device.", ModalYesNo, (localChange) =>
                                                                {
                                                                    if (localChange == PromptResult.Success)
                                                                    {
                                                                        if (accountManager.RenameAccount(name))
                                                                        {
                                                                            MessageBox(MessageKind.Default, $"The local account name was renamed '{name}'.");
                                                                        }
                                                                        else
                                                                        {
                                                                            MessageBox(MessageKind.Error, $"Was not possible to rename the local account.\nHowever the public address was renamed with success.");
                                                                        }
                                                                    }
                                                                });
                                                            }
                                                        }
                                                        else
                                                        {
                                                            MessageBox(MessageKind.Error, "An error occured when trying to setup the address name.");
                                                        }
                                                    });

                                                }
                                            });
                                        }
                                        else
                                        {
                                            MessageBox(MessageKind.Error, "That name is not a valid Phantasma address name.\nNo spaces allowed, only lowercase letters and numbers.\nMust be between 3 and 15 characters in length.");
                                        }
                                    }
                                });
                            }
                            else
                            {
                                MessageBox(MessageKind.Error, $"To register an address name you will need at least some SOUL staked.");
                            }
                            break;
                        }

                    case 2:
                        {
                            ShowModal("Account migration", "Insert WIF of the target account", ModalState.Input, 32, 64, ModalConfirmCancel, 1, (wifResult, wif) =>
                            {
                                if (wifResult != PromptResult.Success)
                                {
                                    return; // user cancelled
                                }

                                var newKeys = PhantasmaKeys.FromWIF(wif);
                                if (newKeys.Address.Text != accountManager.CurrentState.address)
                                {
                                    PromptBox("Are you sure you want to migrate this account?\nTarget address: "+newKeys.Address.Text, ModalYesNo, (result) =>
                                    {
                                        if (result == PromptResult.Success)
                                        {
                                            var address = Address.FromText(accountManager.CurrentState.address);

                                            var sb = new ScriptBuilder();
                                            var gasPrice = accountManager.Settings.feePrice;

                                            sb.AllowGas(address, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                            sb.CallContract("validator", "Migrate", address, newKeys.Address);
                                            sb.SpendGas(address);
                                            var script = sb.EndScript();

                                            SendTransaction("Migrate account", script, DomainSettings.RootChainName, (hash) =>
                                            {
                                                if (hash != Hash.Null)
                                                {
                                                    accountManager.ReplaceAccountWIF(accountManager.CurrentIndex, wif);
                                                    AudioManager.Instance.PlaySFX("click");
                                                    CloseCurrentStack();
                                                    MessageBox(MessageKind.Default, "The account was migrated.");
                                                }
                                                else
                                                {
                                                    MessageBox(MessageKind.Error, "It was not possible to migrate the account.");
                                                }
                                            });
                                        }
                                    });
                                }
                                else
                                {
                                    MessageBox(MessageKind.Error, "You need to provide a different WIF.");
                                }

                            });
                            break;
                        }

                    case 3:
                        {
                            PromptBox("Are you sure you want to delete this account?\nYou can only restore it if you made a backup of the private keys.", ModalConfirmCancel, (result) =>
                            {
                                if (result == PromptResult.Success)
                                {
                                    RequestPassword("Account Deletion", accountManager.CurrentAccount.platforms, (delete) =>
                                    {
                                        if (delete == PromptResult.Success)
                                        {
                                            accountManager.DeleteAccount(accountManager.CurrentIndex);
                                            AudioManager.Instance.PlaySFX("click");
                                            CloseCurrentStack();
                                            MessageBox(MessageKind.Default, "The account was deleted.");
                                        }
                                        else 
                                        if (delete == PromptResult.Failure)
                                        {
                                            MessageBox(MessageKind.Error, "Auth failed.");
                                        }
                                    });
                                }
                            });
                        }
                        break;
                }
            });

            DoBottomMenu();
        }

        private void DrawDropshadow(Rect rect)
        {
            float percent = 1/8f;
            var padX = rect.width * percent;
            var padY = rect.height * percent;
            var dropRect = new Rect(rect.x - padX, rect.y - padY, rect.width + padX * 2, rect.height + padY * 2);
            GUI.DrawTexture(dropRect, ResourceManager.Instance.Dropshadow);
        }

        private string[] accountMenu = new string[] { "Export WIF", "Setup Name", "Migrate", "Delete Account" };
        private GUIState[] bottomMenu = new GUIState[] { GUIState.Balances, GUIState.History, GUIState.Account, GUIState.Exit };

        private int DoBottomMenu()
        {
            int posY;
            DoButtonGrid<GUIState>(false, bottomMenu.Length, 0, out posY, (index) =>
            {
                var btnKind = bottomMenu[index];
                return new MenuEntry(btnKind, btnKind.ToString(), btnKind != this.guiState);
            },
            (selected) =>
            {
                PushState(selected);
            });

            return posY;
        }

        private Action<Hash> transactionCallback;

        private void InvokeTransactionCallback(Hash hash)
        {
            var temp = transactionCallback;
            transactionCallback = null;
            temp?.Invoke(hash);
        }

        private void SendTransaction(string description, byte[] script, string chain, Action<Hash> callback)
        {
            if (script == null)
            {
                MessageBox(MessageKind.Error, "Null transaction script", () =>
                {
                    callback(Hash.Null);
                });
            }

            var accountManager = AccountManager.Instance;
            if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
            {
                BigInteger usedGas;

                try
                {
                    var vm = new GasMachine(script);
                    var result = vm.Execute();
                    usedGas = vm.UsedGas;
                }
                catch
                {
                    usedGas = 400;
                }

                var estimatedFee = usedGas * accountManager.Settings.feePrice;
                var feeDecimals = accountManager.GetTokenDecimals("KCAL");
                description += $"\nEstimated fee: {UnitConversion.ToDecimal(estimatedFee, feeDecimals)} KCAL";
            }

            RequestPassword(description, accountManager.CurrentPlatform, (auth) =>
            {
                if (auth == PromptResult.Success)
                {
                    Animate(AnimationDirection.Right, true, () =>
                    {
                        PushState(GUIState.Sending);

                        Animate(AnimationDirection.Left, false, () =>
                        {
                            var temp = $"Preparing Transaction...\n{description}";

                            MessageBox(MessageKind.Default, temp, () =>
                            {
                                accountManager.SignAndSendTransaction(chain, script, (hash, error) =>
                                {
                                    if (hash != Hash.Null)
                                    {
                                        ShowConfirmationScreen(hash, callback);
                                    }
                                    else
                                    {
                                        PopState();

                                        MessageBox(MessageKind.Error, $"Error sending transaction.\n{error}", () =>
                                        {
                                            InvokeTransactionCallback(hash);
                                        });
                                    }
                                });
                            });
                        });
                    });
                }
                else
                if (auth == PromptResult.Failure)
                {
                    MessageBox(MessageKind.Error, $"Authorization failed.");
                }
            });
        }

        private void ShowConfirmationScreen(Hash hash, Action<Hash> callback)
        {
            transactionCallback = callback;
            needsConfirmation = true;
            confirmationCount = 0;
            transactionHash = hash;
            lastTransactionConfirmation = DateTime.UtcNow;
            
            if (guiState == GUIState.Sending)
            {
                SetState(GUIState.Confirming);
            }
            else
            {
                PushState(GUIState.Confirming);
            }
        }

        #region transfers
        private void ContinuePhantasmaTransfer(string transferName, string symbol, string destAddress)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Phantasma)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Phantasma);
                return;
            }

            var source = Address.FromText(state.address);
            var destination = Address.FromText(destAddress);

            if (source == destination)
            {
                MessageBox(MessageKind.Error, $"Source and destination address must be different!");
                return;
            }

            var balance = state.GetAvailableAmount(symbol);
            RequireAmount(transferName, destAddress, symbol, 0.1m, balance, (amount) =>
            {
                RequestKCAL(symbol, (feeResult) =>
                {
                    if (feeResult == PromptResult.Success)
                    {
                        byte[] script;

                        try
                        {
                            var decimals = accountManager.GetTokenDecimals(symbol);

                            var gasPrice = accountManager.Settings.feePrice;

                            var sb = new ScriptBuilder();
                            sb.AllowGas(source, Address.Null, gasPrice, AccountManager.MinGasLimit);

                            if (symbol == "KCAL" && amount == balance)
                            {
                                sb.TransferBalance(symbol, source, destination);
                            }
                            else
                            {
                                sb.TransferTokens(symbol, source, destination, UnitConversion.ToBigInteger(amount, decimals));
                            }

                            sb.SpendGas(source);
                            script = sb.EndScript();
                        }
                        catch (Exception e)
                        {
                            MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message);
                            return;
                        }

                        SendTransaction($"Transfer {amount} {symbol}\nDestination: {destination}", script, "main", (hash) =>
                        {
                            if (hash != Hash.Null)
                            {
                                MessageBox(MessageKind.Success, $"You transfered {amount} {symbol}!\nTransaction hash:\n" + hash);
                            }
                        });
                    }
                    else
                    if (feeResult == PromptResult.Failure)
                    {
                        MessageBox(MessageKind.Error, $"KCAL is required to make transactions!");
                    }
                });
            });
        }

        private bool ValidDecimals(decimal amount, string symbol)
        {
            var decimals = AccountManager.Instance.GetTokenDecimals(symbol);

            if (decimals > 0)
            {
                return true;
            }

            var temp = amount - (long)amount;
            return temp == 0;
        }

        private void RequireAmount(string description, string destination, string symbol, decimal min, decimal max, Action<decimal> callback)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;
            var caption = $"Enter {symbol} amount:\nMax: {max} {symbol}";
            if (!string.IsNullOrEmpty(destination))
            {
                caption += $"\nDestination: {destination}";
            }

            ShowModal(description, caption, ModalState.Input, 1, 64, ModalConfirmCancel, 1, (result, temp) =>
            {
                if (result == PromptResult.Failure)
                {
                    return; // user cancelled
                }

                decimal amount = ParseNumber(temp);

                if (amount > 0 && ValidDecimals(amount, symbol))
                {
                    if (amount > max)
                    {
                        MessageBox(MessageKind.Error, $"Not enough {symbol}!");
                        return;
                    }
                    else
                    if (amount < min)
                    {
                        MessageBox(MessageKind.Error, $"Amount is too small.\nMinimum accepted is {min} {symbol}!");
                        return;
                    }
                    else
                    {
                        callback(amount);
                    }
                }
                else
                {
                    MessageBox(MessageKind.Error, "Invalid amount!");
                    return;
                }
            });
        }

        private void ContinueNeoTransfer(string transferName, string symbol, string destAddress)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Neo)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Neo);
                return;
            }

            var sourceAddress = accountManager.GetAddress(accountManager.CurrentIndex, accountManager.CurrentPlatform);

            if (sourceAddress == destAddress)
            {
                MessageBox(MessageKind.Error, $"Source and destination address must be different!");
                return;
            }

            var gasBalance = accountManager.CurrentState.GetAvailableAmount("GAS");
            if (gasBalance <= 0)
            {
                MessageBox(MessageKind.Error, $"You will need at least a drop of GAS in this wallet to make a transaction.");
                return;
            }

            var balance = state.GetAvailableAmount(symbol);
            RequireAmount(transferName, destAddress, symbol, 0.01m, balance, (amount) =>
            {
                var transfer = new TransferRequest()
                {
                    platform = PlatformKind.Neo,
                    amount = amount,
                    symbol = symbol,
                    key = accountManager.CurrentAccount.WIF,
                    destination = destAddress
                };

                byte[] script = Serialization.Serialize(transfer);

                SendTransaction($"Transfer {amount} {symbol}\nDestination: {destAddress}", script, transfer.platform.ToString(), (hash) =>
                {
                    if (hash != Hash.Null)
                    {
                        MessageBox(MessageKind.Success, $"You transfered {amount} {symbol}!\nTransaction hash: " + hash);
                    }
                });
            });
        }

        private void ContinueSwap(PlatformKind destPlatform, string transferName, string symbol, string destination)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            var sourceAddress = accountManager.GetAddress(accountManager.CurrentIndex, accountManager.CurrentPlatform);

            if (sourceAddress == destination)
            {
                MessageBox(MessageKind.Error, $"Source and destination address must be different!");
                return;
            }

            var feeSymbol = "GAS";

            var min = 0.001m;
            RequestFee(symbol, feeSymbol, min, (gasResult) =>
            {
                if (gasResult != PromptResult.Success)
                {
                    MessageBox(MessageKind.Error, $"Without at least {min} {feeSymbol} it is not possible to perform this swap!");
                    return;
                }

                var balance = state.GetAvailableAmount(symbol);
                RequireAmount(transferName, null, symbol, 0.1m, balance, (amount) =>
                {
                    if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                    {
                        Address destAddress;

                        switch (destPlatform)
                        {
                            case PlatformKind.Neo:
                                destAddress = AccountManager.EncodeNeoAddress(destination);
                                break;

                            default:
                                MessageBox(MessageKind.Error, $"Swaps to {destPlatform} are not possible yet.");
                                break;
                        }

                        RequestKCAL(symbol, (feeResult) =>
                        {
                            if (feeResult == PromptResult.Success)
                            {
                                byte[] script;

                                var source = Address.FromText(sourceAddress);

                                try
                                {
                                    var decimals = accountManager.GetTokenDecimals(symbol);

                                    var gasPrice = accountManager.Settings.feePrice;

                                    var sb = new ScriptBuilder();
                                    sb.AllowGas(source, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                    sb.TransferTokens(symbol, source, destAddress, UnitConversion.ToBigInteger(amount, decimals));
                                    sb.SpendGas(source);
                                    script = sb.EndScript();
                                }
                                catch (Exception e)
                                {
                                    MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message);
                                    return;
                                }

                                SendTransaction($"Transfer {amount} {symbol}\nDestination: {destAddress}", script, "main", (hash) =>
                                {
                                    if (hash != Hash.Null)
                                    {
                                        MessageBox(MessageKind.Success, $"You transfered {amount} {symbol}!\nTransaction hash:\n" + hash);
                                    }
                                });
                            }
                            else
                            if (feeResult == PromptResult.Failure)
                            {
                                MessageBox(MessageKind.Error, $"KCAL is required to make transactions!");
                            }
                        });
                    }
                    else
                    {
                        accountManager.FindInteropAddress(accountManager.CurrentPlatform, (interopAddress) =>
                        {
                            if (!string.IsNullOrEmpty(interopAddress))
                            {
                                Debug.Log("Found interop address: " + interopAddress);

                                var transfer = new TransferRequest()
                                {
                                    platform = accountManager.CurrentPlatform,
                                    amount = amount,
                                    symbol = symbol,
                                    key = accountManager.CurrentAccount.WIF,
                                    destination = interopAddress,
                                    interop = destination,
                                };

                                byte[] script = Serialization.Serialize(transfer);

                                SendTransaction($"Transfer {amount} {symbol}\nDestination: {destination}", script, transfer.platform.ToString(), (hash) =>
                                {
                                    if (hash != Hash.Null)
                                    {
                                        MessageBox(MessageKind.Success, $"You transfered {amount} {symbol}!\nTransaction hash: " + hash);
                                    }
                                });
                            }
                            else
                            {
                                MessageBox(MessageKind.Error, "Could not fetch interop address");
                            }
                        });
                    }
                });
            });
        }

        private void RequestKCAL(string swapSymbol, Action<PromptResult> callback)
        {
            RequestFee(swapSymbol, "KCAL", 0.1m, callback);
        }

        private void RequestFee(string swapSymbol, string feeSymbol, decimal min, Action<PromptResult> callback)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            if (swapSymbol == "NEO")
            {
                swapSymbol = "GAS";
            }

            decimal feeBalance = state.GetAvailableAmount(feeSymbol);

            if (accountManager.CurrentPlatform != PlatformKind.Phantasma)
            {
                callback(feeBalance >= min ? PromptResult.Success : PromptResult.Failure);
                return;
            }

            if (swapSymbol == feeSymbol)
            {
                callback(PromptResult.Success);
                return;
            }

            if (feeBalance >= min)
            {
                callback(PromptResult.Success);
                return;
            }

            if (swapSymbol == null)
            {
                MessageBox(MessageKind.Error, $"Not enough {feeSymbol} for transaction fees.", () =>
                {
                    callback(PromptResult.Failure);
                });                
                return;
            }

            var swapDecimals = accountManager.GetTokenDecimals(swapSymbol);
            decimal swapBalance = state.GetAvailableAmount(swapSymbol);

            if (swapDecimals> 0 || swapBalance > 1)
            {
                PromptBox($"Not enough {feeSymbol} for transaction fees.\nUse some {swapSymbol} to perform a cosmic swap?", ModalYesNo,
                     (result) =>
                     {
                         if (result == PromptResult.Success)
                         {
                             byte[] script;

                             try
                             {
                                 var source = Address.FromText(state.address);

                                 var gasPrice = accountManager.Settings.feePrice;

                                 var decimals = accountManager.GetTokenDecimals(feeSymbol);

                                 var sb = new ScriptBuilder();
                                 if (feeSymbol == "KCAL")
                                 {
                                     sb.CallContract("swap", "SwapFee", source, swapSymbol, UnitConversion.ToBigInteger(0.5m, decimals));
                                 }
                                 else
                                 {
                                     sb.CallContract("swap", "SwapReverse", source, swapSymbol, feeSymbol, UnitConversion.ToBigInteger(0.5m, decimals));
                                 }
                                 sb.AllowGas(source, Address.Null, gasPrice, AccountManager.MinGasLimit);
                                 sb.SpendGas(source);
                                 script = sb.EndScript();
                             }
                             catch (Exception e)
                             {
                                 MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message);
                                 return;
                             }

                             SendTransaction($"Swap {swapSymbol} for {feeSymbol}", script, "main", (hash) =>
                             {
                                 callback(hash != Hash.Null ? PromptResult.Success : PromptResult.Failure);
                             });
                         }
                         else
                         {
                             callback(result);
                         }
                     });
            }
            else
            {
                MessageBox(MessageKind.Error, $"Not enough {feeSymbol} for transaction fees.\nHowever to use {swapSymbol} cosmic swaps, you need at least 2 {swapSymbol}.");
            }
        }
        #endregion


        private void GetAccountHints(PlatformKind targets)
        {
            var accountManager = AccountManager.Instance;
            var hints = new Dictionary<string, string>();

            hints["Scan QR"] = $"|{GUIState.ScanQR}";

            for (int index=0; index< accountManager.Accounts.Length; index++)
            {
                var account = accountManager.Accounts[index];
                var platforms = account.platforms.Split();

                foreach (var platform in platforms)
                {
                    if (account.name == accountManager.CurrentAccount.name && platform == accountManager.CurrentPlatform)
                    {
                        continue;
                    }

                    if (targets.HasFlag(platform))
                    {
                        var addr = accountManager.GetAddress(index, platform);
                        if (!string.IsNullOrEmpty(addr))
                        {
                            var key = $"{account.name} [{platform}]";
                            hints[key] = addr;
                        }
                    }
                }
            }

            modalHints = hints;
        }

        #region QR CODES
        public Texture2D GenerateQR(string text)
        {
            var encoded = new Texture2D(256, 256);
            var color32 = EncodeQR(text, encoded.width, encoded.height);
            encoded.SetPixels32(color32);
            encoded.Apply();
            return encoded;
        }

        private static Color32[] EncodeQR(string textForEncoding, int width, int height)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width
                }
            };
            return writer.Write(textForEncoding);
        }
        #endregion

        private decimal ParseNumber(string s)
        {
            s = s.Trim().Replace(" ", "").Replace("_", "");
            s = s.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
            decimal result;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return -1;
        }
    }

}
