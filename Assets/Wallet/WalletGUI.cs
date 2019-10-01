using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Phantasma.VM.Utils;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Contracts;
using Phantasma.Numerics;
using System.Linq;

namespace Poltergeist
{
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
        public const string MoneyFormat = "0.####";

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

        private int currencyIndex;
        private string[] currencyOptions;
        private ComboBox currencyComboBox = new ComboBox();

        private ComboBox platformComboBox = new ComboBox();

        private ComboBox hintComboBox = new ComboBox();

        private int nexusIndex;
        private ComboBox nexusComboBox = new ComboBox();

        private NexusKind[] availableNexus = Enum.GetValues(typeof(NexusKind)).Cast<NexusKind>().ToArray();

        public static int Units(int n)
        {
            return 16 * n;
        }

        void Start()
        {
            int border = Units(4);
            windowRect.width = Mathf.Min(800, Screen.width) - border;
            windowRect.height = Mathf.Min(800, Screen.height) - border;

            windowRect.x = (Screen.width - windowRect.width) / 2;
            windowRect.y = (Screen.height - windowRect.height) / 2;

            defaultRect = new Rect(windowRect);

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

            switch (state)
            {
                case GUIState.Balances:
                    accountManager.RefreshBalances(false);
                    break;

                case GUIState.History:
                    accountManager.RefreshHistory(false);
                    break;

                case GUIState.Settings:
                    {
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

        public void MessageBox(string caption, Action callback = null)
        {
            ShowModal("Attention", caption, ModalState.Message, 0, false, null, (result, input) =>
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
                    stateStack.Clear();
                    guiState = GUIState.Loading;
                    PushState(GUIState.Accounts);
                    Animate(AnimationDirection.Down, false);
                });
            }

            if (currentAnimation != AnimationDirection.None)
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

            if (modalResult != PromptResult.Waiting)
            {
                var temp = modalCallback;
                var result = modalResult;
                var success = modalResult == PromptResult.Success;
                modalState = ModalState.None;
                modalCallback = null;
                modalResult = PromptResult.Waiting;
                temp?.Invoke(result, success ? modalInput : null);
            }
        }

        void OnGUI()
        {
            GUI.skin = guiSkin;
            GUI.Window(0, windowRect, DoMainWindow, "Poltergeist Wallet");

            if (modalState != ModalState.None)
            {
                var modalWidth = Units(44);
                var modalHeight = Units(14 + 2 * modalLineCount);
                modalRect = new Rect((Screen.width - modalWidth) / 2, (Screen.height - modalHeight) / 2, modalWidth, modalHeight);
                modalRect = GUI.ModalWindow(0, modalRect, DoModalWindow, modalTitle);
            }
        }

        private Rect GetExpandedRect(int curY, int height)
        {
            int border = Units(1);
            var rect = new Rect(border, curY, windowRect.width - border * 2, height);
            return rect;
        }

        private void DoMainWindow(int windowID)
        {
            GUI.DrawTexture(new Rect(Units(1), Units(1) + 4, 32, 32), ResourceManager.Instance.WalletLogo);

            switch (guiState)
            {
                case GUIState.Loading:
                    DrawCenteredText(AccountManager.Instance.Ready ? "Starting..." : AccountManager.Instance.Status);
                    break;

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

            GUI.Label(new Rect(rect.x, curY, rect.width, Units(2* modalLineCount)), modalCaption);
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
                    modalResult = PromptResult.Failure;
                }

                GUI.enabled = modalState != ModalState.Input || modalInput.Length > 0;
                if (GUI.Button(new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), "Confirm"))
                {
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
                            MessageBox(msg, () =>
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
                    MessageBox($"Could not open '{account.name}' account");
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
                        MessageBox("Error creating account.\n" + e.Message);
                    }
                }
            });
        }

        private void DoAccountScreen()
        {
            int curY = Units(5);

            var accountManager = AccountManager.Instance;
            for (int i = 0; i < accountManager.Accounts.Length; i++)
            {
                var account = accountManager.Accounts[i];

                var panelHeight = Units(8);
                var rect = GetExpandedRect(curY, panelHeight);
                GUI.Box(rect, "");

                int btnWidth = Units(7);
                int halfWidth = (int)(rect.width / 2);

                GUI.Label(new Rect(Units(2), curY + Units(1), Units(25), Units(2)), account.ToString());

                if (GUI.Button(new Rect(windowRect.width - (btnWidth + Units(2) + 4), curY + Units(2) - 4, btnWidth, Units(2)), "Open"))
                {
                    LoginIntoAccount(i);
                }

                curY += Units(6);
            }

            // import account panel on bottom
            {
                var panelHeight = Units(9);
                curY = (int)(windowRect.height - panelHeight + Units(1));
                var rect = GetExpandedRect(curY, panelHeight);
                GUI.Box(rect, "");

                int btnWidth = Units(11);
                int halfWidth = (int)(rect.width / 2);

                GUI.Label(new Rect(halfWidth - 10, curY + Units(3), 28, 20), "or");

                if (GUI.Button(new Rect(-Units(2) + (halfWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Generate new wallet"))
                {
                    var keys = PhantasmaKeys.Generate();
                    CreateWallet(keys.ToWIF());
                }

                if (GUI.Button(new Rect((rect.width - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Import private key"))
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
                                        MessageBox("Could not import wallet.\n"+e.Message);
                                    }
                                }
                            });
                        }
                        else
                        {
                            MessageBox("Invalid private key");
                        }
                    });
                }

                if (GUI.Button(new Rect(Units(2) + halfWidth + (halfWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Settings"))
                {
                    Animate(AnimationDirection.Up, true, () =>
                    {
                        PushState(GUIState.Settings);
                        Animate(AnimationDirection.Down, false);
                    });
                }
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

        private void DoCloseButton(Func<bool> callback = null)
        {
            if (GUI.Button(new Rect(windowRect.width - Units(3), Units(1), Units(2), Units(2)), "X"))
            {
                if (callback == null || callback())
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
            }
        }

        private void DoSettingsScreen()
        {
            var accountManager = AccountManager.Instance;
            var settings = accountManager.Settings;

            DoCloseButton(() =>
            {
                if (!settings.phantasmaRPCURL.IsValidURL())
                {
                    MessageBox("Invalid URL for Phantasma RPC URL\n" + settings.phantasmaRPCURL);
                    return false;
                }

                if (!settings.neoRPCURL.IsValidURL())
                {
                    MessageBox("Invalid URL for Phantasma RPC URL\n" + settings.neoRPCURL);
                    return false;
                }

                if (!settings.neoscanURL.IsValidURL())
                {
                    MessageBox("Invalid URL for Phantasma RPC URL\n" + settings.neoscanURL);
                    return false;
                }

                if (settings.feePrice < 1)
                {
                    MessageBox("Invalid value for fee price\n" + settings.feePrice);
                    return false;
                }

                accountManager.RefreshTokenPrices();
                accountManager.Settings.Save();
                return true;
            });

            int curY = Units(2);

            int headerSize = Units(10);
            GUI.Label(new Rect((windowRect.width - headerSize) / 2, curY, headerSize, Units(2)), "SETTINGS");
            curY += Units(5);

            var fieldWidth = Units(20);

            int dropHeight;

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Currency");
            currencyIndex = currencyComboBox.Show(new Rect(Units(11), curY, Units(8), Units(2)), currencyOptions, out dropHeight);
            settings.currency = currencyOptions[currencyIndex];
            curY += dropHeight + Units(1);

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Nexus");

            var nexusList = availableNexus.Select(x => x.ToString().Replace('_', ' ')).ToArray();
            var prevNexus = nexusIndex;
            nexusIndex = nexusComboBox.Show(new Rect(Units(11), curY, Units(8), Units(2)), nexusList, out dropHeight);
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
        }

        private void DrawPlatformTopMenu(string caption)
        {
            DoCloseButton();

            var accountManager = AccountManager.Instance;

            int currentPlatformIndex = 0;
            var platformList = accountManager.CurrentAccount.platforms.Split();

            int curY = Units(2);

            DrawHorizontalCenteredText(curY, Units(2), caption);

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
                var platformIndex = platformComboBox.Show(new Rect(Units(3) + 8, curY - 8, Units(8), Units(2)), platformList, out dropHeight);

                if (platformIndex != currentPlatformIndex)
                {
                    accountManager.CurrentPlatform = platformList[platformIndex];
                }
            }

            var state = accountManager.CurrentState;
            if (state == null)
            {
                return;
            }

            curY += Units(5);

            DrawHorizontalCenteredText(curY - 5, Units(2), state.address);

#if UNITY_EDITOR
            if (GUI.Button(new Rect(windowRect.width - Units(6), curY + 5, Units(4), Units(1)), "Copy"))
            {
                EditorGUIUtility.systemCopyBuffer = state.address;
                MessageBox("Address copied to clipboard");
            }
#endif
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

            /*
                case WalletState.Error:
                    DrawCenteredText("Error fetching balances...");
                    DoCloseButton();
                    */


            Rect rect;
            int panelHeight;

            DrawPlatformTopMenu("BALANCES");

            var state = accountManager.CurrentState;

            if (state == null)
            {
                DrawCenteredText("Temporary error, cannot display balances...");
                return;
            }

            if (state.flags.HasFlag(AccountFlags.Master) && ResourceManager.Instance.MasterLogo != null)
            {
                GUI.DrawTexture(new Rect(Units(1), Units(5), Units(8), Units(8)), ResourceManager.Instance.MasterLogo);
            }

            int curY = Units(12);

            decimal feeBalance = state.GetAvailableAmount("KCAL");

            int balanceCount = 0;
            int btnWidth;
            int index = 0;
            foreach (var balance in state.balances)
            {
                if (balance.Total < 0.001m)
                {
                    continue;
                }

                balanceCount++;
                var icon = ResourceManager.Instance.GetToken(balance.Symbol);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(Units(2), curY + Units(1), Units(2), Units(2)), icon);
                }

                panelHeight = Units(8);
                rect = GetExpandedRect(curY, panelHeight);
                GUI.Box(rect, "");

                btnWidth = Units(11);
                int halfWidth = (int)(rect.width / 2);

                var posY = curY + Units(1) - 8;
                GUI.Label(new Rect(Units(5), posY, Units(20), Units(2)), $"{balance.Available.ToString(MoneyFormat)} {balance.Symbol} ({accountManager.GetTokenWorth(balance.Symbol, balance.Available)})");

                var subRect = new Rect(Units(5), posY + Units(1) + 4, Units(20), Units(2));
                DrawBalanceLine(ref subRect, balance.Symbol, balance.Staked, "staked");
                DrawBalanceLine(ref subRect, balance.Symbol, balance.Pending, "pending");
                DrawBalanceLine(ref subRect, balance.Symbol, balance.Claimable, "claimable");

                string secondaryAction;
                bool secondaryEnabled;
                Action secondaryCallback;

                switch (balance.Symbol)
                {
                    case "SOUL":
                        if (balance.Staked > 0)
                        {
                            secondaryAction = "Unstake";
                            secondaryEnabled = true;
                            secondaryCallback = () =>
                            {
                                ConfirmBox($"Do you want to unstake {balance.Staked} SOUL?\nYou won't be able to claim KCAL anymore.", (result) =>
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
                                                    MessageBox("Your SOUL was unstaked!\nTransaction hash: " + hash);
                                                }
                                            });
                                        }
                                    });
                                });

                            };
                        }
                        else
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
                                                    MessageBox("Your SOUL was staked!\nTransaction hash: " + hash);
                                                }
                                            });
                                        }
                                    });
                                });

                            };
                        }
                        break;

                    case "KCAL":
                        secondaryAction = "Claim";
                        secondaryEnabled = balance.Claimable > 0;
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
                                            MessageBox("You claimed some KCAL!\nTransaction hash: " + hash);
                                        }
                                    });
                                }
                            });
                        };
                        break;

                    case "GAS":
                        secondaryAction = "Claim";
                        secondaryEnabled = balance.Claimable > 0;
                        secondaryCallback = () =>
                        {
                        };
                        break;

                    default:
                        secondaryAction = null;
                        secondaryEnabled = false;
                        secondaryCallback = null;
                        break;
                }

                if (!string.IsNullOrEmpty(secondaryAction))
                {
                    GUI.enabled = secondaryEnabled;
                    if (GUI.Button(new Rect(rect.x + rect.width - Units(17) -4, curY + Units(1), Units(4)+8, Units(2)), secondaryAction))
                    {
                        secondaryCallback?.Invoke();
                    }
                    GUI.enabled = true;
                }

                if (balance.Symbol == "KCAL")
                {
                    if (GUI.Button(new Rect(rect.x + rect.width - Units(11) -4 , curY + Units(1), Units(4)+8, Units(2)), "Burn"))
                    {
                        var amountText = balance.Available.ToString(MoneyFormat);
                        ConfirmBox($"Do you want to burn {amountText} KCAL?\nIt will be sent to the SES energy bomb!", (result) =>
                        {
                            if (result == PromptResult.Success)
                            {
                                var address = Address.FromText(state.address);

                                var burnAddress = Address.FromHash("bomb");

                                var sb = new ScriptBuilder();
                                var gasPrice = accountManager.Settings.feePrice;

                                sb.AllowGas(address, Address.Null, gasPrice, 9999);
                                sb.TransferBalance(balance.Symbol, address, burnAddress);
                                sb.SpendGas(address);
                                var script = sb.EndScript();

                                SendTransaction($"Burn {amountText} KCAL", script, "main", (hash) =>
                                {
                                    if (hash != Hash.Null)
                                    {
                                        MessageBox("Your burned some KCAL!\nTransaction hash: " + hash);
                                    }
                                });
                            }
                        });
                    }
                }
                else
                {
                    var swapEnabled = AccountManager.Instance.SwapSupported(balance.Symbol);
                    GUI.enabled = swapEnabled;
                    GUI.Button(new Rect(rect.x + rect.width - Units(11)-4, curY + Units(1), Units(4)+8, Units(2)), "Swap");
                    GUI.enabled = true;
                }

                if (GUI.Button(new Rect(rect.x + rect.width - (Units(5) + 8)- 4, curY + Units(1), Units(4)+8, Units(2)), "Send"))
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
                                ContinueSwapOut(transferName, transferSymbol, destAddress);
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
                                ContinueSwapIn(transferName, transferSymbol, destAddress);
                            }
                            else
                            {
                                MessageBox($"Direct transfers from {accountManager.CurrentPlatform} to this type of address not supported");
                            }
                        }
                        else
                        {
                            MessageBox("Invalid destination address");
                        }
                    });
                    break;
                }

                curY += Units(6);
                index++;
            }


            if (balanceCount == 0)
            {
                DrawHorizontalCenteredText(curY, Units(2), $"No assets found in this {accountManager.CurrentPlatform} account.");
            }

            if (guiState != GUIState.Balances)
            {
                return;
            }

            DoBottomMenu();
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

        private void DoBottomMenu()
        {
            int panelHeight = Units(9);
            int curY = (int)(windowRect.height - panelHeight);

            curY += Units(1);
            var rect = GetExpandedRect(curY, panelHeight);

            int buttonCount = bottomMenu.Length;

            int divisionWidth = (int)(rect.width / buttonCount);
            int btnWidth = (int)(divisionWidth * 0.8f);
            int padding = (divisionWidth - btnWidth) / 2;

            for (int i = 0; i < buttonCount; i++)
            {
                var btnKind = bottomMenu[i];

                GUI.enabled = btnKind != this.guiState;
                if (GUI.Button(new Rect((Units(1) / 2) + 4 + padding + i * divisionWidth, curY + Units(3), btnWidth, Units(2)), btnKind.ToString()))
                {
                    PushState(btnKind);
                    return;
                }
                GUI.enabled = true;
            }
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
                                transactionCallback = callback;
                                needsConfirmation = true;
                                transactionHash = hash;
                                lastTransactionConfirmation = DateTime.UtcNow;
                                SetState(GUIState.Confirming);
                            }
                            else
                            {
                                PopState();

                                MessageBox("Error sending transaction", () =>
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
                    MessageBox($"Authorization failed");
                }
            });
        }

        #region transfers
        private void ContinuePhantasmaTransfer(string transferName, string symbol, string destAddress)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            var source = Address.FromText(state.address);
            var destination = Address.FromText(destAddress);

            if (source == destination)
            {
                MessageBox($"Source and destination address must be different!");
                return;
            }

            ShowModal(transferName, $"Enter {symbol} amount", ModalState.Input, 64, true, null, (result, temp) =>
            {
                if (result == PromptResult.Failure)
                {
                    return; // user cancelled
                }

                decimal amount;

                if (decimal.TryParse(temp, out amount) && amount > 0)
                {
                    var balance = state.GetAvailableAmount(symbol);

                    if (amount > balance)
                    {
                        MessageBox($"Not enough {symbol}!");
                        return;
                    }
                    else
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
                                    MessageBox("Something went wrong!\n" + e.Message);
                                    return;
                                }

                                SendTransaction($"Transfer {amount} {symbol}", script, "main", (hash) =>
                                {
                                    if (hash != Hash.Null)
                                    {
                                        MessageBox($"You transfered {amount} {symbol}!\nTransaction hash: " + hash);
                                    }
                                });
                            }
                            else
                            if (feeResult == PromptResult.Failure)
                            {
                                MessageBox($"KCAL is required to make transactions!");
                            }
                        });
                    }
                }
                else
                {
                    MessageBox("Invalid amount!");
                    return;
                }
            });
        }


        private void ContinueNeoTransfer(string transferName, string symbol, string destAddress)
        {
            MessageBox("Not implemented :(");
        }

        private void ContinueSwapIn(string transferName, string symbol, string destAddress)
        {
            MessageBox("Not implemented :(");
        }

        private void ContinueSwapOut(string transferName, string symbol, string destAddress)
        {
            MessageBox("Not implemented :(");
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
                            MessageBox("Something went wrong!\n" + e.Message);
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
