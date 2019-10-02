using System;
using System.Collections.Generic;
using UnityEngine;
using Phantasma.VM.Utils;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Contracts;
using Phantasma.Numerics;
using System.Linq;
using Phantasma.Storage;
using Phantasma.Domain;
using UnityEngine.UI;

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
        Accounts,
        Balances,
        History,
        Sending,
        Confirming,
        Settings,
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

        public const string MoneyFormat = "0.####";

        public int Border => Units(1);
        public int HalfBorder => Border/2;
        public const bool fullScreen = true;

        public GUISkin guiSkin;

        private Rect windowRect = new Rect(0, 0, 600, 400);
        private Rect defaultRect;

        private Rect modalRect;

        private GUIState guiState;
        private Stack<GUIState> stateStack = new Stack<GUIState>();

        private string transferSymbol;
        private Hash transactionHash;
        private bool needsConfirmation;
        private DateTime lastTransactionConfirmation;

        private AnimationDirection currentAnimation;
        private float animationTime;
        private bool invertAnimation;
        private Action animationCallback;

        private bool HasAnimation => currentAnimation != AnimationDirection.None;

        private string currentTitle;

        private int currencyIndex;
        private string[] currencyOptions;
        private ComboBox currencyComboBox = new ComboBox();

        private ComboBox platformComboBox = new ComboBox();

        private ComboBox hintComboBox = new ComboBox();

        private int nexusIndex;
        private ComboBox nexusComboBox = new ComboBox();

        private NexusKind[] availableNexus = Enum.GetValues(typeof(NexusKind)).Cast<NexusKind>().ToArray();

        private bool initialized;

        private bool smallSize => windowRect.width <= 420;

        public static int Units(int n)
        {
            return 16 * n;
        }

        void Start()
        {
            initialized = false;

            guiState = GUIState.Loading;

            currencyOptions = AccountManager.Instance.Currencies.ToArray();
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
            guiState = state;

            var accountManager = AccountManager.Instance;

            currentTitle = null;

            switch (state)
            {
                case GUIState.Accounts:
                    currentTitle = "Accounts";
                    accountScroll = Vector2.zero;
                    break;

                case GUIState.Balances:
                    currentTitle = "Balances";
                    balanceScroll = Vector2.zero;
                    accountManager.RefreshBalances(false);
                    break;

                case GUIState.History:
                    currentTitle = "History";
                    accountManager.RefreshHistory(false);
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
            }

            if (currentTitle != null)
            {
                currentTitle = currentTitle.ToUpper();
            }
        }

        private void PopState()
        {
            guiState = stateStack.Pop();
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
        private float modalTime;
        private ModalState modalState;
        private Action<PromptResult, string> modalCallback;
        private string modalInput;
        private int modalInputLength;
        private string modalCaption;
        private string modalTitle;
        private bool modalAllowCancel;
        private Dictionary<string, string> modalHints;
        private PromptResult modalResult;
        private int modalLineCount;

        private void ShowModal(string title, string caption, ModalState state, int maxInputLength, bool allowCancel, Dictionary<string, string> hints, Action<PromptResult, string> callback)
        {
            if (modalState == ModalState.None)
            {
                modalTime = Time.time;
            }

            modalResult = PromptResult.Waiting;
            modalInput = "";
            modalState = state;
            modalTitle = title;
            modalInputLength = maxInputLength;
            modalCaption = caption;
            modalCallback = callback;
            modalAllowCancel = allowCancel;
            modalHints = hints;
            hintComboBox.SelectedItemIndex = -1;
            modalLineCount = 1 + modalCaption.Where(x => x == '\n').Count();
        }

        public void ConfirmBox(string caption, Action<PromptResult> callback)
        {
            ShowModal("Confirmation", caption, ModalState.Message, 0, true, null, (result, input) =>
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

            ShowModal(title, caption, ModalState.Message, 0, false, null, (result, input) =>
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
            ShowModal("Account Authorization", $"Account: {accountManager.CurrentAccount.name} ({platforms})\nAction: {description}\n\nInsert password to proceed...", ModalState.Password, Account.MaxPasswordLength, true, null, (result, input) =>
            {
                if (result == PromptResult.Success && !string.IsNullOrEmpty(input) && input == accountManager.CurrentAccount.password)
                {
                    callback(PromptResult.Success);
                }
                else
                {
                    callback(result);
                }
            });
        }
        #endregion

        private void Update()
        {
            if (this.guiState == GUIState.Loading && AccountManager.Instance.Ready && !HasAnimation)
            {
                Animate(AnimationDirection.Up, true, () =>
                {
                    AudioManager.Instance.PlaySFX("load");

                    stateStack.Clear();
                    PushState(GUIState.Accounts);

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
                        windowRect.x = Mathf.Lerp(Screen.width + defaultRect.width, defaultRect.x, delta);
                        break;

                    case AnimationDirection.Up:
                        windowRect.y = Mathf.Lerp(-defaultRect.height, defaultRect.y, delta);
                        break;

                    case AnimationDirection.Down:
                        windowRect.y = Mathf.Lerp(Screen.height + defaultRect.height, defaultRect.y, delta);
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
                    windowRect.width = Screen.width - Border * 2;
                    windowRect.height = Screen.height - Border * 2;
                }
                else
                {
                    background.texture = null;
                    windowRect.width = Mathf.Min(800, Screen.width) - Border * 2;
                    windowRect.height = Mathf.Min(800, Screen.height) - Border * 2;
                }

                windowRect.x = (Screen.width - windowRect.width) / 2;
                windowRect.y = (Screen.height - windowRect.height) / 2;

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
                temp?.Invoke(result, success ? modalInput : null);

                if (modalState == ModalState.None)
                {
                    modalTime = Time.time;
                }
            }
        }

        void OnGUI()
        {
            GUI.skin = guiSkin;
            GUI.enabled = true;

            GUI.color = Color.white;

            var duration = 0.333f;
            var delta = (Time.time - modalTime) / duration;
            if (delta > 1.0f)
            {
                delta = 1;
            }

            if (modalState == ModalState.None)
            {
                delta = 1 - delta;
            }

            if (modalState != ModalState.None)
            {
                var k = Mathf.Lerp(1, 0.4f, delta);
                GUI.color = new Color(1, 1, 1, k);
            }

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
                    GUI.Window(0, windowRect, DoMainWindow, "Poltergeist Wallet");
                }
            }

            GUI.color = Color.white;

            if (modalState != ModalState.None)
            {
                var modalWidth = Units(44);
                var modalHeight = Units(26);
                modalRect = new Rect((Screen.width - modalWidth) / 2, (Screen.height - modalHeight) / 2, modalWidth, modalHeight);
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
            GUI.Box(new Rect(8, 8, windowRect.width - 16, Units(2)), "");
            GUI.Label(new Rect(windowRect.width / 2 + Units(7), 0, 32, Units(2)), Application.version);

            if (currentTitle != null)
            {
                int curY = Units(3);
                DrawHorizontalCenteredText(curY, Units(2), currentTitle);
            }

            switch (guiState)
            {
                case GUIState.Sending:
                    DrawCenteredText("Sending transaction...");
                    break;

                case GUIState.Confirming:
                    DoConfirmingScreen();
                    break;

                case GUIState.Accounts:
                    DoAccountScreen();
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
            }

            //GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }

        private void DoModalWindow(int windowID)
        {
            var accountManager = AccountManager.Instance;

            int curY = Units(4);

            var rect = new Rect(Units(1), curY, modalRect.width - Units(2), modalRect.height - Units(2));

            GUI.Label(new Rect(rect.x, curY, rect.width, Units(3 * modalLineCount)), modalCaption);
            curY += Units(2);

            var fieldWidth = rect.width;

            bool hasHints = modalHints != null && modalHints.Count > 0;
            int hintWidth = Units(10);

            if (hasHints)
            {
                fieldWidth -= hintWidth + Units(1);
            }

            curY += Units(modalLineCount) + 4 * modalLineCount;
            int hintY = curY;

            if (modalState == ModalState.Input)
            {
                modalInput = GUI.TextField(new Rect(rect.x, curY, fieldWidth, Units(2)), modalInput, modalInputLength);
            }
            else
            if (modalState == ModalState.Password)
            {
                modalInput = GUI.PasswordField(new Rect(rect.x, curY, fieldWidth, Units(2)), modalInput, '*', modalInputLength);
            }

            int btnWidth = Units(11);

            curY = (int)(rect.height - Units(2));

            if (modalAllowCancel)
            {
                int halfWidth = (int)(rect.width / 2);

                if (GUI.Button(new Rect((halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), "Cancel"))
                {
                    AudioManager.Instance.PlaySFX("cancel");
                    modalResult = PromptResult.Failure;
                }

                GUI.enabled = modalState != ModalState.Input || modalInput.Length > 0;
                if (GUI.Button(new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), "Confirm"))
                {
                    AudioManager.Instance.PlaySFX("click");
                    modalResult = PromptResult.Success;
                }
                GUI.enabled = true;
            }
            else
            {
                if (GUI.Button(new Rect((rect.width - btnWidth) / 2, curY, btnWidth, Units(2)), "Ok"))
                {
                    modalResult = PromptResult.Success;
                }
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
                        modalInput = modalHints[key];
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
                if (diff.TotalSeconds > 5)
                {
                    lastTransactionConfirmation = now;
                    needsConfirmation = false;
                    accountManager.RequestConfirmation(transactionHash.ToString(), (msg) =>
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
                        if (msg == "pending")
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

        private void LoginIntoAccount(int index)
        {
            var accountManager = AccountManager.Instance;
            accountManager.SelectAccount(index);
            RequestPassword("Open wallet", accountManager.CurrentAccount.platforms, (auth) =>
            {
                if (auth == PromptResult.Success)
                {
                    accountManager.RefreshTokenPrices();
                    Animate(AnimationDirection.Down, true, () => {
                        PushState(GUIState.Balances);
                        Animate(AnimationDirection.Up, false);
                    });
                }
                else
                if (auth == PromptResult.Failure)
                {
                    var account = accountManager.Accounts[index];
                    MessageBox(MessageKind.Error, $"Could not open '{account.name}' account");
                }
            });
        }

        private void CreateWallet(string wif)
        {
            ShowModal("Wallet Name", "Enter name for your wallet", ModalState.Input, 32, true, null, (result, name) =>
            {
                if (result == PromptResult.Success)
                {
                    try
                    {
                        var accountManager = AccountManager.Instance;
                        int walletIndex = accountManager.AddWallet(name, PlatformKind.Phantasma | PlatformKind.Neo, wif);
                        LoginIntoAccount(walletIndex);
                    }
                    catch (Exception e)
                    {
                        MessageBox(MessageKind.Error, "Error creating account.\n" + e.Message);
                    }
                }
            });
        }


        private string[] accountOptions = new string[] { "Generate new wallet", "Import private key", "Settings" };

        private Vector2 accountScroll;
        private Vector2 balanceScroll;

        private void DoAccountScreen()
        {
            int endY;
            DoButtonGrid<int>(true, accountOptions.Length, out endY, (index) =>
            {
                return new MenuEntry(index, accountOptions[index], true);
            },
            (selected) =>
            {
                switch (selected)
                {
                    case 0:
                        {
                            var keys = PhantasmaKeys.Generate();
                            CreateWallet(keys.ToWIF());
                            break;
                        }

                    case 1:
                        {
                            ShowModal("Wallet Import", "Enter your private key", ModalState.Input, 64, true, null, (result, key) =>
                            {
                                if (key.Length == 52 && (key.StartsWith("K") || key.StartsWith("L")))
                                {
                                    CreateWallet(key);
                                }
                                else
                                if (key.Length == 58 && key.StartsWith("6"))
                                {
                                    ShowModal("NEP2 Encrypted Key", "Insert your wallet passphrase", ModalState.Password, 64, true, null, (auth, passphrase) =>
                                    {
                                        if (auth == PromptResult.Success)
                                        {
                                            try
                                            {
                                                var decryptedKeys = Phantasma.Neo.Core.NeoKeys.FromNEP2(key, passphrase);
                                                CreateWallet(decryptedKeys.WIF);
                                            }
                                            catch (Exception e)
                                            {
                                                MessageBox(MessageKind.Error, "Could not import wallet.\n" + e.Message);
                                            }
                                        }
                                    });
                                }
                                else
                                {
                                    MessageBox(MessageKind.Error, "Invalid private key");
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

            int smallOfs = smallSize ? 1 : 0;

            var accountManager = AccountManager.Instance;

            int startY = (int)(windowRect.y + Units(4));

            int panelHeight = Units(6);

            DoScrollArea<Account>(ref accountScroll, startY, endY, panelHeight, accountManager.Accounts,
                (account, index, curY, rect) =>
                {
                    int btnWidth = Units(7);
                    int halfWidth = (int)(rect.width / 2);

                    GUI.Label(new Rect(Units(1), curY + Units(1) - 16 * smallOfs, Units(25), Units(2)), account.ToString());

                    if (GUI.Button(new Rect(rect.width - (btnWidth + Units(2) + 4), curY + Units(2) - 4 + smallOfs * 8, btnWidth, Units(2)), "Open"))
                    {
                        LoginIntoAccount(index);
                    }
                });
        }

        // returns total items
        private int DoScrollArea<T>(ref Vector2 scroll, int startY, int endY, int panelHeight, IEnumerable<T> items, Action<T, int, int, Rect> callback)
        {
            int panelWidth = (int)(windowRect.width - (Border * 2));

            var itemCount = items.Count();
            var insideRect = new Rect(0, 0, panelWidth, ((panelHeight + Border) * (itemCount - 1) + panelHeight));
            var outsideRect = new Rect(Border, startY, panelWidth, endY - (startY + Border));

            bool needsScroll = insideRect.height > outsideRect.height;
            if (needsScroll)
            {
                panelWidth -= Border;
                insideRect.width = panelWidth;
            }

            int curY = 0;

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

        private void DoButtonGrid<T>(bool showBackground, int buttonCount, out int posY, Func<int, MenuEntry> options, Action<T> callback)
        {
            bool vertical = smallSize;

            var border = Units(1);

            int panelHeight = vertical ? (Units(3) + 4) *  buttonCount : (border + Units(3));
            posY = (int)((windowRect.y + windowRect.height) - (panelHeight+ border*3));

            var rect = new Rect(border, posY, windowRect.width - border * 2, panelHeight);
            
            if (showBackground)
            {
                GUI.Box(rect, "");
            }

            int divisionWidth = (int)(rect.width / buttonCount);
            int btnWidth = (int)(divisionWidth * 0.8f);
            int padding = (divisionWidth - btnWidth) / 2;

            T selected = default(T);
            bool hasSelection = false;

            for (int i = 0; i < buttonCount; i++)
            {
                var entry = options(i);

                Rect btnRect;

                if (vertical)
                {
                    btnRect = new Rect(rect.x + border, rect.y + border + i * (Units(3)+4), rect.width - border * 2, Units(2));
                }
                else
                {
                    btnRect = new Rect((Units(1) / 2) + 4 + padding + i * divisionWidth, rect.y + border, btnWidth, Units(2));
                }

                GUI.enabled = entry.enabled;
                if (GUI.Button(btnRect, entry.label))
                {
                    hasSelection = true;
                    selected = (T)entry.value;
                }
                GUI.enabled = true;
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

            GUI.Label(new Rect(0, 0, windowRect.width, windowRect.height), caption);

            style.alignment = temp;
        }

        private void DrawHorizontalCenteredText(int curY, float height, string caption)
        {
            var style = GUI.skin.label;
            var temp = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;

            GUI.Label(new Rect(0, curY, windowRect.width, height), caption);

            style.alignment = temp;
        }

        private void CloseCurrentStack()
        {
            Animate(AnimationDirection.Down, true, () =>
            {
                var accountManager = AccountManager.Instance;
                accountManager.UnselectAcount();
                stateStack.Clear();
                PushState(GUIState.Accounts);

                Animate(AnimationDirection.Up, false);
            });
        }

        private void DoCloseButton(Func<bool> callback = null)
        {
            if (GUI.Button(new Rect(windowRect.width - Units(4), Units(1) + 2, Units(2) - 4, Units(1) - 4), "X"))
            {
                if (callback == null || callback())
                {
                    CloseCurrentStack();
                }
            }
        }

        private bool ValidateSettings()
        {
            var accountManager = AccountManager.Instance;
            var settings = accountManager.Settings;

            if (settings.nexusKind == NexusKind.Unknown)
            {
                MessageBox(MessageKind.Error, "Select a Phantasma network first\n" + settings.phantasmaRPCURL);
                return false;
            }

            if (!settings.phantasmaRPCURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma RPC URL\n" + settings.phantasmaRPCURL);
                return false;
            }

            if (!settings.neoRPCURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma RPC URL\n" + settings.neoRPCURL);
                return false;
            }

            if (!settings.neoscanURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma RPC URL\n" + settings.neoscanURL);
                return false;
            }

            if (settings.feePrice < 1)
            {
                MessageBox(MessageKind.Error, "Invalid value for fee price\n" + settings.feePrice);
                return false;
            }

            accountManager.InitDemoAccounts(settings.nexusKind);

            accountManager.RefreshTokenPrices();
            accountManager.Settings.Save();
            return true;
        }

        private void DoSettingsScreen()
        {
            var accountManager = AccountManager.Instance;
            var settings = accountManager.Settings;

            if (settings.nexusKind != NexusKind.Unknown)
            {
                DoCloseButton(ValidateSettings);
            }

            int curY = Units(7);

            var fieldWidth = Units(20);

            int dropHeight;

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Currency");
            currencyIndex = currencyComboBox.Show(new Rect(Units(11), curY, Units(8), Units(2)), currencyOptions, out dropHeight);
            settings.currency = currencyOptions[currencyIndex];
            curY += dropHeight + Units(1);

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Nexus");

            var nexusList = availableNexus.Select(x => x.ToString().Replace('_', ' ')).ToArray();
            var prevNexus = nexusIndex;
            nexusIndex = nexusComboBox.Show(new Rect(Units(11), curY, Units(8), Units(2)), nexusList, out dropHeight, null, 1);
            settings.nexusKind = availableNexus[nexusIndex];
            curY += dropHeight + Units(1);

            if (prevNexus != nexusIndex && settings.nexusKind != NexusKind.Custom)
            {
                settings.RestoreEndpoints();
            }

            bool hasCustomEndPoints;
            bool hasCustomFee;

            switch (settings.nexusKind)
            {
                case NexusKind.Custom:
                case NexusKind.Local_Net:
                    {
                        hasCustomEndPoints = true;
                        hasCustomFee = true;
                        break;
                    }

                case NexusKind.Test_Net:
                    {
                        hasCustomEndPoints = false;
                        hasCustomFee = true;
                        break;
                    }

                default:
                    {
                        hasCustomEndPoints = false;
                        hasCustomFee = false;
                        break;
                    }
            }

            if (hasCustomEndPoints)
            {
                GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Phantasma RPC URL");
                settings.phantasmaRPCURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.phantasmaRPCURL);
                curY += Units(3);

                GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Neo RPC URL");
                settings.neoRPCURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.neoRPCURL);
                curY += Units(3);

                GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Neoscan API URL");
                settings.neoscanURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.neoscanURL);
                curY += Units(3);
            }
            else
            {
                settings.RestoreEndpoints();
            }

            if (hasCustomFee)
            {
                GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Fee price");
                var fee = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.feePrice.ToString());
                BigInteger.TryParse(fee, out settings.feePrice);
                curY += Units(3);
            }

            if (accountManager.Accounts.Length > 0)
            {
                curY += Units(1);
                if (GUI.Button(new Rect(Units(1), curY, Units(16), Units(2)), "Delete Everything"))
                {
                    ConfirmBox("All wallets and settings stored in this device will be lost.\nMake sure you have backups of your private keys!\nOtherwise you will lose access to your funds.", (result) =>
                    {
                        accountManager.DeleteAll();
                        PlayerPrefs.DeleteAll();
                        accountManager.Settings.Load();
                        MessageBox(MessageKind.Default, "All data removed from this device.");
                    });
                }
                curY += Units(3);
            }

            if (accountManager.Accounts.Length == 0)
            {
                var btnWidth = Units(10);
                curY = (int)(windowRect.height - Units(4));
                if (GUI.Button(new Rect((windowRect.width - btnWidth) / 2, curY, btnWidth, Units(2)), "Continue"))
                {
                    if (ValidateSettings())
                    {
                        CloseCurrentStack();
                    }
                }
            }
        }

        private int DrawPlatformTopMenu(string caption)
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

            if (mainToken != null)
            {
                GUI.DrawTexture(new Rect(Units(1) + 8, Units(1) - 4, 24, 24), ResourceManager.Instance.GetToken(mainToken));
            }

            DoCloseButton();

            int currentPlatformIndex = 0;
            var platformList = accountManager.CurrentAccount.platforms.Split();

            int curY = Units(1);

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
                var platformIndex = platformComboBox.Show(new Rect(Units(3) + 8, curY, Units(8), Units(1)), platformList, out dropHeight);

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

            curY = Units(5);

            DrawHorizontalCenteredText(curY - 5, Units(2), state.address);

            if (GUI.Button(new Rect(windowRect.width - Units(6), curY + 5, Units(4), Units(1)), "Copy"))
            {
                GUIUtility.systemCopyBuffer = state.address;
                MessageBox(MessageKind.Default, "Address copied to clipboard!");
            }

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
                style.fontSize = 18;

                GUI.Label(subRect, $"{amount.ToString(MoneyFormat)} {symbol} {caption} ({AccountManager.Instance.GetTokenWorth(symbol, amount)})");
                style.fontSize = tempSize;
                style.normal.textColor = tempColor;

                subRect.y += 12;
            }
        }

        private void DoBalanceScreen()
        {
            var accountManager = AccountManager.Instance;

            if (accountManager.Refreshing)
            {
                DrawCenteredText("Fetching balances...");
                return;
            }

            var startY = DrawPlatformTopMenu("BALANCES");
            var endY = DoBottomMenu();

            var state = accountManager.CurrentState;

            if (state == null)
            {
                DrawCenteredText("Temporary error, cannot display balances...");
                return;
            }

            if (state.flags.HasFlag(AccountFlags.Master) && ResourceManager.Instance.MasterLogo != null)
            {
                GUI.DrawTexture(new Rect(Units(1), Units(2), Units(8), Units(8)), ResourceManager.Instance.MasterLogo);
            }

            int curY = Units(12);

            decimal feeBalance = state.GetAvailableAmount("KCAL");

            var balanceCount = DoScrollArea<Balance>(ref balanceScroll, startY, endY, Units(5), state.balances.Where(x => x.Total >= 0.001m), 
                DoBalanceEntry);

            if (balanceCount == 0)
            {
                DrawHorizontalCenteredText(curY, Units(2), $"No assets found in this {accountManager.CurrentPlatform} account.");
            }
        }

        private void DoBalanceEntry(Balance balance, int index, int curY, Rect rect)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            var icon = ResourceManager.Instance.GetToken(balance.Symbol);
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(Units(2), curY + Units(1), Units(2), Units(2)), icon);
            }

            int panelHeight = Units(8);
            GUI.Box(rect, "");

            int btnWidth = Units(11);
            int halfWidth = (int)(rect.width / 2);

            var posY = curY + Units(1) - 8;
            GUI.Label(new Rect(Units(5), posY, Units(20), Units(2)), $"{balance.Available.ToString(MoneyFormat)} {balance.Symbol} ({accountManager.GetTokenWorth(balance.Symbol, balance.Available)})");

            var subRect = new Rect(Units(5), posY + Units(1) + 4, Units(20), Units(2));
            DrawBalanceLine(ref subRect, balance.Symbol, balance.Staked, "staked");
            DrawBalanceLine(ref subRect, balance.Symbol, balance.Pending, "pending");
            DrawBalanceLine(ref subRect, balance.Symbol, balance.Claimable, "claimable");

            string secondaryAction = null;
            bool secondaryEnabled = false;
            Action secondaryCallback = null;

            if (balance.Pending > 0)
            {
                secondaryAction = "Claim";
                secondaryEnabled = true;
                secondaryCallback = () =>
                {
                    ConfirmBox($"You have {balance.Pending} {balance.Symbol} pending in your account.\nDo you want to claim it?", (result) =>
                    {
                        accountManager.SettleSwap(balance.PendingPlatform, accountManager.CurrentPlatform.ToString().ToLower(), balance.PendingHash, (settleHash) =>
                        {
                            ShowConfirmationScreen(settleHash, (hash) =>
                            {
                                if (hash != Hash.Null)
                                {
                                    MessageBox(MessageKind.Success, $"Your {balance.Symbol} arrived in your {accountManager.CurrentPlatform} account.");
                                }
                                else
                                {
                                    MessageBox(MessageKind.Error, "There was some error confirming the transaction...");
                                }
                            });
                        });
                    });
                };
            }
            else
                switch (balance.Symbol)
                {
                    case "SOUL":
                        if (balance.Staked > 0)
                        {
                            secondaryAction = "Unstake";
                            secondaryEnabled = true;
                            secondaryCallback = () =>
                            {
                                RequireAmount("Unstake SOUL", "SOUL", balance.Staked,
                                    (amount) =>
                                    {
                                        var line = amount == balance.Staked ? "You won't be able to claim KCAL anymore." : "The amount of KCAL that will be able to claim later will be reduced.";

                                        ConfirmBox($"Do you want to unstake {amount} SOUL?\n{line}", (result) =>
                                        {
                                            RequestKCAL("SOUL", (kcal) =>
                                            {
                                                if (kcal == PromptResult.Success)
                                                {
                                                    var address = Address.FromText(state.address);

                                                    var sb = new ScriptBuilder();
                                                    var gasPrice = accountManager.Settings.feePrice;

                                                    sb.AllowGas(address, Address.Null, gasPrice, 9999);
                                                    sb.CallContract("stake", "Unstake", address, UnitConversion.ToBigInteger(balance.Staked, balance.Decimals));
                                                    sb.AllowGas(address, Address.Null, gasPrice, 9999);

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
                        else
                            if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                        {
                            secondaryAction = "Stake";
                            secondaryEnabled = balance.Available > 1.2m;
                            secondaryCallback = () =>
                            {
                                var stakeAmount = Math.Min(balance.Available, AccountManager.SoulMasterStakeAmount);

                                var expectedDailyKCAL = stakeAmount * 0.002m;

                                ConfirmBox($"Do you want to stake {stakeAmount} SOUL?\nYou will be able to claim {expectedDailyKCAL} KCAL per day.", (result) =>
                                {
                                    RequestKCAL("SOUL", (kcal) =>
                                    {
                                        if (kcal == PromptResult.Success)
                                        {
                                            var address = Address.FromText(state.address);

                                            var sb = new ScriptBuilder();
                                            var gasPrice = accountManager.Settings.feePrice;

                                            sb.AllowGas(address, Address.Null, gasPrice, 9999);
                                            sb.CallContract("stake", "Stake", address, UnitConversion.ToBigInteger(stakeAmount, balance.Decimals));
                                            sb.AllowGas(address, Address.Null, gasPrice, 9999);

                                            sb.SpendGas(address);
                                            var script = sb.EndScript();

                                            SendTransaction($"Stake {stakeAmount} SOUL", script, "main", (hash) =>
                                            {
                                                if (hash != Hash.Null)
                                                {
                                                    MessageBox(MessageKind.Success, "Your SOUL was staked!\nTransaction hash: " + hash);
                                                }
                                            });
                                        }
                                    });
                                });

                            };
                        }
                        break;

                    case "KCAL":
                        if (balance.Claimable > 0)
                        {
                            secondaryAction = "Claim";
                            secondaryEnabled = true;
                            secondaryCallback = () =>
                            {
                                ConfirmBox($"Do you want to claim KCAL?\nThere is {balance.Claimable} KCAL available.", (result) =>
                                {
                                    if (result == PromptResult.Success)
                                    {
                                        var address = Address.FromText(state.address);
                                        var gasPrice = accountManager.Settings.feePrice;

                                        var sb = new ScriptBuilder();
                                        sb.AllowGas(address, Address.Null, gasPrice, 1);
                                        sb.CallContract("stake", "Claim", address, address);
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
                                RequireAmount("Burning KCAL", "KCAL", balance.Available, (amount) =>
                                {
                                    var amountText = amount.ToString(MoneyFormat);
                                    ConfirmBox($"Do you want to burn {amountText} KCAL?\nIt will be sent to the SES energy bomb!", (result) =>
                                    {
                                        if (result == PromptResult.Success)
                                        {
                                            var address = Address.FromText(state.address);

                                            var burnAddress = Address.FromHash("bomb");

                                            var sb = new ScriptBuilder();
                                            var gasPrice = accountManager.Settings.feePrice;

                                            sb.AllowGas(address, Address.Null, gasPrice, 999);
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
                                    MessageBox(MessageKind.Error, "Not supported yet");
                                };
                            }
                            break;
                        }
                }

            if (!string.IsNullOrEmpty(secondaryAction))
            {
                GUI.enabled = secondaryEnabled;
                if (GUI.Button(new Rect(rect.x + rect.width - Units(12), curY + Units(1), Units(4) + 8, Units(2)), secondaryAction))
                {
                    secondaryCallback?.Invoke();
                }
                GUI.enabled = true;
            }

            GUI.enabled = balance.Available > 0;
            if (GUI.Button(new Rect(rect.x + rect.width - Units(6), curY + Units(1), Units(4), Units(2)), "Send"))
            {
                transferSymbol = balance.Symbol;
                var transferName = $"{transferSymbol} transfer";
                ShowModal(transferName, "Enter destination address", ModalState.Input, 64, true, GetAccountHints(accountManager.CurrentPlatform.GetTransferTargets()), (result, destAddress) =>
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
                            MessageBox(MessageKind.Error, $"Direct transfers from {accountManager.CurrentPlatform} to this type of address not supported");
                        }
                    }
                    else
                    {
                        MessageBox(MessageKind.Error, "Invalid destination address");
                    }
                });
            }
            GUI.enabled = true;
        }

        private void DoHistoryScreen()
        {
            var accountManager = AccountManager.Instance;

            if (accountManager.Refreshing)
            {
                DrawCenteredText("Fetching historic...");
                return;
            }

            DrawPlatformTopMenu("TRANSACTION HISTORY");
            int curY = Units(10);

            Rect rect;

            var history = accountManager.CurrentHistory;

            if (history == null)
            {
                DrawCenteredText("Temporary error, cannot display historic...");
                return;
            }

            int panelHeight = Units(3);
            int panelWidth = (int)(windowRect.width - Units(2));
            int padding = 8;

            int availableHeight = (int)(windowRect.height - (curY + Units(6)));
            int heightPerItem = panelHeight + padding;
            int maxEntries = availableHeight / heightPerItem;

            if (history.Length > 0)
            {
                for (int i = 0; i < history.Length; i++)
                {
                    if (i >= maxEntries)
                    {
                        break;
                    }

                    var entry = history[i];

                    var date = String.Format("{0:g}", entry.date);

                    rect = new Rect(Units(1), curY, panelWidth, panelHeight);
                    GUI.Box(rect, "");

                    int halfWidth = (int)(rect.width / 2);

                    GUI.Label(new Rect(Units(3), curY + 4, Units(20), Units(2)), entry.hash);
                    GUI.Label(new Rect(Units(26), curY + 4, Units(20), Units(2)), date);

                    GUI.enabled = !string.IsNullOrEmpty(entry.url);
                    if (GUI.Button(new Rect(windowRect.width - Units(6), curY + 8, Units(4), Units(1)), "View"))
                    {
                        Application.OpenURL(entry.url);
                    }
                    GUI.enabled = true;

                    curY += panelHeight + padding;
                }
            }
            else
            {
                DrawHorizontalCenteredText(curY, Units(2), $"No transactions found for this {accountManager.CurrentPlatform} account.");
            }

            if (guiState != GUIState.History)
            {
                return;
            }

            DoBottomMenu();
        }

        private GUIState[] bottomMenu = new GUIState[] { GUIState.Balances, GUIState.History };

        private int DoBottomMenu()
        {
            int posY;
            DoButtonGrid<GUIState>(false, bottomMenu.Length, out posY, (index) =>
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

        private void DoBackButton()
        {
            int panelHeight = Units(9);
            int curY = (int)(windowRect.height - panelHeight + Units(1));

            var rect = GetExpandedRect(curY, panelHeight);

            int btnWidth = Units(11);

            int totalWidth = (int)rect.width; // (int)(rect.width / 2);

            //GUI.Button(new Rect((halfWidth - btnWidth) / 2, prevY + Units(3), btnWidth, Units(2)), "Something");

            int leftoverWidth = (int)(rect.width - totalWidth);

            if (GUI.Button(new Rect(leftoverWidth + (totalWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Back"))
            {
                PopState();
            }
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
            RequestPassword(description, accountManager.CurrentPlatform, (auth) =>
            {
                if (auth == PromptResult.Success)
                {
                    Animate(AnimationDirection.Right, true, () =>
                    {
                        PushState(GUIState.Sending);

                        accountManager.SignAndSendTransaction(chain, script, (hash) =>
                        {
                            if (hash != Hash.Null)
                            {
                                ShowConfirmationScreen(hash, callback);
                            }
                            else
                            {
                                PopState();

                                MessageBox(MessageKind.Error, "Error sending transaction", () =>
                                {
                                    InvokeTransactionCallback(hash);
                                });
                            }
                        });
                        Animate(AnimationDirection.Left, false);
                    });
                }
                else
                if (auth == PromptResult.Failure)
                {
                    MessageBox(MessageKind.Error, $"Authorization failed");
                }
            });
        }

        private void ShowConfirmationScreen(Hash hash, Action<Hash> callback)
        {
            transactionCallback = callback;
            needsConfirmation = true;
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
            RequireAmount(transferName, symbol, balance, (amount) =>
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
                            sb.AllowGas(source, Address.Null, gasPrice, 300);

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

                        SendTransaction($"Transfer {amount} {symbol}", script, "main", (hash) =>
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

        private void RequireAmount(string description, string symbol, decimal max, Action<decimal> callback)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;
            ShowModal(description, $"Enter {symbol} amount", ModalState.Input, 64, true, null, (result, temp) =>
            {
                if (result == PromptResult.Failure)
                {
                    return; // user cancelled
                }

                decimal amount;

                if (decimal.TryParse(temp.Replace(',', '.'), out amount) && amount > 0 && ValidDecimals(amount, symbol))
                {
                    if (amount > max)
                    {
                        MessageBox(MessageKind.Error, $"Not enough {symbol}!");
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

        private void ContinueNeoTransfer(string transferName, string symbol, string destination)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Neo)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Neo);
                return;
            }

            var sourceAddress = accountManager.CurrentAccount.GetAddress(accountManager.CurrentPlatform);

            if (sourceAddress == destination)
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
            RequireAmount(transferName, symbol, balance, (amount) =>
            {
                var transfer = new TransferRequest()
                {
                    platform = PlatformKind.Neo,
                    amount = amount,
                    symbol = symbol,
                    key = accountManager.CurrentAccount.key,
                    destination = destination
                };

                byte[] script = Serialization.Serialize(transfer);

                SendTransaction($"Transfer {amount} {symbol}", script, transfer.platform.ToString(), (hash) =>
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

            var sourceAddress = accountManager.CurrentAccount.GetAddress(accountManager.CurrentPlatform);

            if (sourceAddress == destination)
            {
                MessageBox(MessageKind.Error, $"Source and destination address must be different!");
                return;
            }


            var feeSymbol = "GAS";
            var gasBalance = accountManager.CurrentState.GetAvailableAmount(feeSymbol);
            if (gasBalance <= 0)
            {
                MessageBox(MessageKind.Error, $"You will need some {feeSymbol} in this wallet to make a swap.");
                return;
            }

            ShowModal(transferName, $"Enter {symbol} amount", ModalState.Input, 64, true, null, (result, temp) =>
            {
                if (result == PromptResult.Failure)
                {
                    return; // user cancelled
                }

                decimal amount;

                if (decimal.TryParse(temp.Replace(',', '.'), out amount) && amount > 0 && ValidDecimals(amount, symbol))
                {
                    var balance = state.GetAvailableAmount(symbol);

                    if (amount > balance)
                    {
                        MessageBox(MessageKind.Error, $"Not enough {symbol}!");
                        return;
                    }
                    else
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
                                        sb.AllowGas(source, Address.Null, gasPrice, 300);

                                        sb.TransferTokens(symbol, source, destination, UnitConversion.ToBigInteger(amount, decimals));

                                        sb.SpendGas(source);
                                        script = sb.EndScript();
                                    }
                                    catch (Exception e)
                                    {
                                        MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message);
                                        return;
                                    }

                                    SendTransaction($"Transfer {amount} {symbol}", script, "main", (hash) =>
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
                                        key = accountManager.CurrentAccount.key,
                                        destination = interopAddress,
                                        interop = destination,
                                    };

                                    byte[] script = Serialization.Serialize(transfer);

                                    SendTransaction($"Transfer {amount} {symbol}", script, transfer.platform.ToString(), (hash) =>
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
                    }
                }
                else
                {
                    MessageBox(MessageKind.Error, "Invalid amount!");
                    return;
                }
            });
        }

        private void RequestKCAL(string swapSymbol, Action<PromptResult> callback)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;
            var source = Address.FromText(state.address);

            if (accountManager.CurrentPlatform != PlatformKind.Phantasma)
            {
                callback(PromptResult.Failure);
                return;
            }

            if (swapSymbol == "KCAL")
            {
                callback(PromptResult.Success);
                return;
            }

            decimal feeBalance = state.GetAvailableAmount("KCAL");

            if (feeBalance > 0.1m)
            {
                callback(PromptResult.Success);
                return;
            }

            var swapDecimals = accountManager.GetTokenDecimals(swapSymbol);

            if (swapDecimals> 0)
            {
                ConfirmBox($"Not enough KCAL for transaction fees.\nUse some {swapSymbol} to perform a cosmic swap?",
                     (result) =>
                     {
                         if (result == PromptResult.Success)
                         {
                             byte[] script;

                             try
                             {
                                 var decimals = accountManager.GetTokenDecimals("KCAL");

                                 var gasPrice = accountManager.Settings.feePrice;

                                 var sb = new ScriptBuilder();
                                 sb.CallContract("swap", "SwapFee", source, swapSymbol, UnitConversion.ToBigInteger(1m, decimals));
                                 sb.AllowGas(source, Address.Null, gasPrice, 250);
                                 sb.SpendGas(source);
                                 script = sb.EndScript();
                             }
                             catch (Exception e)
                             {
                                 MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message);
                                 return;
                             }

                             SendTransaction($"Swap {swapSymbol} for KCAL", script, "main", (hash) =>
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
                MessageBox(MessageKind.Error, $"Not enough KCAL for transaction fees.\nHowever {swapSymbol} is not supported currently in cosmic swaps.");
            }
        }
        #endregion


        private Dictionary<string, string> GetAccountHints(PlatformKind targets)
        {
            var accountManager = AccountManager.Instance;
            var hints = new Dictionary<string, string>();

            foreach (var account in accountManager.Accounts)
            {
                var platforms = account.platforms.Split();
                foreach (var platform in platforms)
                {
                    if (account.name == accountManager.CurrentAccount.name && platform == accountManager.CurrentPlatform)
                    {
                        continue;
                    }

                    if (targets.HasFlag(platform))
                    {
                        var addr = account.GetAddress(platform);
                        if (!string.IsNullOrEmpty(addr))
                        {
                            var key = $"{account.name} [{platform}]";
                            hints[key] = addr;
                        }
                    }
                }
            }

            return hints;
        }
    }

}
