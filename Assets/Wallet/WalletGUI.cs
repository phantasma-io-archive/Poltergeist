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
        Transfer,
        Sending,
        Confirming,
        Settings,
    }

    public enum PromptResult
    {
        Waiting,
        Failure,
        Success
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
        public GUISkin guiSkin;

        private Rect windowRect = new Rect(0, 0, 600, 400);
        private Rect defaultRect;

        private Rect modalRect;

        private GUIState guiState;
        private Stack<GUIState> stateStack = new Stack<GUIState>();

        private string transferSymbol;
        private Hash transactionHash;

        private AnimationDirection currentAnimation;
        private float animationTime;
        private bool invertAnimation;
        private Action animationCallback;

        private bool HasAnimation => currentAnimation != AnimationDirection.None;

        private int currencyIndex;
        private string[] currencyOptions;
        private ComboBox currencyComboBox = new ComboBox();

        private ComboBox platformComboBox = new ComboBox();

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
            guiState = state;

            var accountManager = AccountManager.Instance;

            switch (state)
            {
                case GUIState.Settings:
                    {
                        currencyComboBox.SelectedItemIndex = 0;
                        for (int i=0; i<currencyOptions.Length; i++)
                        {
                            if (currencyOptions[i] == accountManager.Settings.currency)
                            {
                                currencyComboBox.SelectedItemIndex = i;
                                break;
                            }

                        }
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
        private Action<string> modalCallback;
        private string modalInput;
        private int modalInputLength;
        private string modalCaption;
        private string modalTitle;
        private bool modalAllowCancel;
        private PromptResult modalResult;

        private void ShowModal(string title, string caption, ModalState state, int maxInputLength, bool allowCancel, Action<string> callback)
        {
            modalResult = PromptResult.Waiting;
            modalInput = "";
            modalState = state;
            modalTitle = title;
            modalInputLength = maxInputLength;
            modalCaption = caption;
            modalCallback = callback;
            modalAllowCancel = allowCancel;
        }

        public void MessageBox(string caption, Action callback = null)
        {
            ShowModal("Attention", caption, ModalState.Message, 0, false, (input) =>
            {
                callback?.Invoke();
            });
        }

        public void RequestPassword(Action<bool> callback)
        {
            var accountManager = AccountManager.Instance;

            if (!accountManager.HasSelection)
            {
                callback(false);
                return;
            }

            if (string.IsNullOrEmpty(accountManager.CurrentAccount.password))
            {
                callback(true);
                return;
            }

            ShowModal("Account Authorization", "Account: " + accountManager.CurrentAccount.ToString()+"\nInsert password to proceed", ModalState.Password, Account.MaxPasswordLength, true, (input) =>
            {
                var success = !string.IsNullOrEmpty(input) && input == accountManager.CurrentAccount.password;
                callback(success);
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
                var success = modalResult == PromptResult.Success;
                modalState = ModalState.None;
                modalCallback = null;
                modalResult = PromptResult.Waiting;
                temp?.Invoke(success ? modalInput: null);
            }
        }

        void OnGUI()
        {
            GUI.skin = guiSkin;
            GUI.Window(0, windowRect, DoMainWindow, "Poltergeist Wallet");

            if (modalState != ModalState.None)
            {
                var modalWidth = Units(30);
                var modalHeight = Units(20);
                modalRect = new Rect((Screen.width - modalWidth) / 2, (Screen.height - modalHeight) / 2, modalWidth, modalHeight);
                modalRect = GUI.ModalWindow(0, modalRect, DoModalWindow, modalTitle);
            }
        }

        private Rect GetExpandedRect(int curY, int height)
        {
            int border = Units(1);
            var rect =new Rect(border, curY, windowRect.width - border*2, height);
            return rect;
        }

        private void DoMainWindow(int windowID)
        {
            GUI.DrawTexture(new Rect(Units(1), Units(1), 32, 32), ResourceManager.Instance.WalletLogo);

            switch (guiState)
            {
                case GUIState.Loading:
                    DrawCenteredText(AccountManager.Instance.Ready ? "Starting...": AccountManager.Instance.Status);
                    break;

                case GUIState.Sending:
                    DrawCenteredText("Sending transaction...");
                    break;

                case GUIState.Confirming:
                    DrawCenteredText($"Confirming transaction {transactionHash}...");
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

                case GUIState.Transfer:
                    DoTransferScreen();
                    break;
            }

            //GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }

        private void DoModalWindow(int windowID)
        {
            var accountManager = AccountManager.Instance;

            int curY = Units(4);

            var rect = new Rect(Units(1), curY, modalRect.width - Units(2), modalRect.height - Units(2));

            GUI.Label(new Rect(rect.x, curY, rect.width, Units(5)), modalCaption);
            curY += Units(5);

            if (modalState == ModalState.Input)
            {
                modalInput = GUI.TextField(new Rect(rect.x, curY, rect.width, Units(2)), modalInput, modalInputLength);
            }
            else
            if (modalState == ModalState.Password)
            {
                modalInput = GUI.PasswordField(new Rect(rect.x, curY, rect.width, Units(2)), modalInput, '*', modalInputLength);
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

                if (GUI.Button(new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), "Confirm"))
                {
                    modalResult = PromptResult.Success;
                }
            }
            else
            {
                if (GUI.Button(new Rect((rect.width - btnWidth) / 2, curY, btnWidth, Units(2)), "Ok"))
                {
                    modalResult = PromptResult.Success;
                }
            }
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

                int btnWidth = Units(11);
                int halfWidth = (int)(rect.width / 2);

                GUI.Label(new Rect(Units(2), curY + Units(1), Units(25), Units(2)), account.ToString());

                if (GUI.Button(new Rect(windowRect.width - (btnWidth + Units(2)), curY + Units(2), btnWidth, Units(2)), "Open"))
                {
                    accountManager.SelectAccount(i);
                    RequestPassword((sucess) =>
                    {
                        if (sucess)
                        {
                            accountManager.RefreshTokenPrices();
                            Animate(AnimationDirection.Down, true, () => {
                                PushState(GUIState.Balances);

                                accountManager.RefreshBalances(() =>
                                {
                                });

                                Animate(AnimationDirection.Up, false);
                            });
                        }
                        else
                        {
                            MessageBox($"Could not open '{account.name}' account");
                        }
                    });
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

                GUI.Button(new Rect(-Units(2) + (halfWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Generate new wallet");
                GUI.Button(new Rect((rect.width - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Import private key");

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

            var size = new Vector2(windowRect.width, windowRect.height); 
            GUI.Label(new Rect((windowRect.width - size.x) / 2, (windowRect.height - size.y) / 2, size.x, size.y), caption);

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

                if (!settings.neoscanAPIURL.IsValidURL())
                {
                    MessageBox("Invalid URL for Phantasma RPC URL\n" + settings.neoscanAPIURL);
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

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Phantasma RPC URL");
            settings.phantasmaRPCURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.phantasmaRPCURL);
            curY += Units(3);

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Neo RPC URL");
            settings.neoRPCURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.neoRPCURL);
            curY += Units(3);

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Neoscan API URL");
            settings.neoscanAPIURL = GUI.TextField(new Rect(Units(11), curY, fieldWidth, Units(2)), settings.neoscanAPIURL);
            curY += Units(3);

            GUI.Label(new Rect(Units(1), curY, Units(8), Units(2)), "Currency");
            currencyIndex = currencyComboBox.Show(new Rect(Units(11), curY, Units(8), Units(2)), currencyOptions);
            accountManager.Settings.currency = currencyOptions[currencyIndex];
            curY += Units(3);
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

            DoCloseButton();

            int curY = Units(2);

            int headerSize = Units(10);
            GUI.Label(new Rect((windowRect.width - headerSize) / 2, curY, headerSize, Units(2)), "BALANCES");
            curY += Units(5);


            Rect rect;
            int panelHeight;

            decimal feeBalance = 0;

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

                var platformIndex = platformComboBox.Show(new Rect(Units(1), curY, Units(8), Units(2)), platformList);

                if (platformIndex != currentPlatformIndex)
                {
                    accountManager.CurrentPlatform = platformList[platformIndex];
                }
            }

            var state = accountManager.CurrentState;

            if (state == null)
            {
                DrawCenteredText("Temporary error, cannot display balances...");
                return;
            }

            GUI.Label(new Rect(Units(10), curY - 5, Units(20), Units(2)), state.address);

            if (GUI.Button(new Rect(Units(10) + state.address.Length * 9, curY + 5, Units(3), Units(1)), "Copy"))
            {
                EditorGUIUtility.systemCopyBuffer = state.address;
                MessageBox("Address copied to clipboard");
            }

            curY += Units(5);

            foreach (var balance in state.balances)
            {
                if (balance.Symbol == "KCAL")
                {
                    feeBalance += balance.Amount;
                }
            }


            int btnWidth;
            int index = 0;
            foreach (var balance in state.balances)
            {
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

                GUI.Label(new Rect(Units(5), curY + Units(1), Units(20), Units(2)), $"{balance.Amount} {balance.Symbol} ({accountManager.GetTokenWorth(balance.Symbol, balance.Amount)})");

                string secondaryAction;
                bool secondaryEnabled;
                Action secondaryCallback;

                switch (balance.Symbol)
                {
                    case "SOUL":
                        secondaryAction = "Stake";
                        secondaryEnabled = state.stake == 0 && balance.Amount > 0;
                        secondaryCallback = () =>
                        {
                            var address = Address.FromText(state.address);

                            var sb = new ScriptBuilder();

                            if (feeBalance > 0)
                            {
                                sb.AllowGas(address, Address.Null, 1, 9999);
                                sb.CallContract("stake", "Stake", address, UnitConversion.ToBigInteger(balance.Amount, balance.Decimals));
                            }
                            else
                            {
                                sb.CallContract("stake", "Stake", address, UnitConversion.ToBigInteger(balance.Amount, balance.Decimals));
                                sb.CallContract("stake", "Claim", address, address);
                                sb.AllowGas(address, Address.Null, 1, 9999);
                            }

                            sb.SpendGas(address);
                            var script = sb.EndScript();

                            SendTransaction(script, "main");
                        };
                        break;

                    case "KCAL":
                        secondaryAction = "Claim";
                        secondaryEnabled = state.claim > 0;
                        secondaryCallback = () =>
                        {
                            var address = Address.FromText(state.address);

                            var sb = new ScriptBuilder();
                            sb.AllowGas(address, Address.Null, 1, 9999);
                            sb.CallContract("stake", "Claim", address, address);
                            sb.SpendGas(address);
                            var script = sb.EndScript();

                            SendTransaction(script, "main");
                        };
                        break;

                    case "GAS":
                        secondaryAction = "Claim";
                        secondaryEnabled = state.claim > 0;
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
                    if (GUI.Button(new Rect(rect.x + rect.width - Units(17), curY + Units(1), Units(4), Units(2)), secondaryAction))
                    {
                        secondaryCallback?.Invoke();
                    }
                    GUI.enabled = true;
                }

                var swapEnabled = AccountManager.Instance.SwapSupported(balance.Symbol);
                GUI.enabled = swapEnabled;
                GUI.Button(new Rect(rect.x + rect.width - Units(11), curY + Units(1), Units(4), Units(2)), "Swap");
                GUI.enabled = true;

                if (GUI.Button(new Rect(rect.x + rect.width - (Units(5) + 8), curY + Units(1), Units(4), Units(2)), "Send"))
                {
                    transferSymbol = balance.Symbol;
                    PushState(GUIState.Transfer);
                    break;
                }

                curY += Units(6);
                index++;
            }

            if (guiState != GUIState.Balances)
            {
                return;
            }

            panelHeight = Units(9);
            curY = (int)(windowRect.height - panelHeight + Units(1));

            rect = GetExpandedRect(curY, panelHeight);
            GUI.Box(rect, state.address);

            btnWidth = Units(11);

            int totalWidth = (int)rect.width; // (int)(rect.width / 2);

            int leftoverWidth = (int)(rect.width - totalWidth);

            if (GUI.Button(new Rect(leftoverWidth + (totalWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Copy Address"))
            {
                EditorGUIUtility.systemCopyBuffer = state.address;
            }
        }

        private void DoTransferScreen()
        {
            DoCloseButton();

            int curY = Units(4);

            int headerSize = Units(10);
            GUI.Label(new Rect((windowRect.width - headerSize) / 2, curY, headerSize, Units(2)), transferSymbol+" TRANSFER");
            curY += Units(3);

            var accountManager = AccountManager.Instance;

            DoBackButton();
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

        private void SendTransaction(byte[] script, string chain)
        {
            var accountManager = AccountManager.Instance;

            Animate(AnimationDirection.Right, true, () =>
            {
                PushState(GUIState.Sending);
                accountManager.SignAndSendTransaction("chain", script, (hash) =>
                {
                    if (hash != Hash.Null)
                    {
                        transactionHash = hash;
                        PushState(GUIState.Confirming);
                    }
                    else
                    {
                        Debug.LogError("Error sending tx");
                    }
                });
                Animate(AnimationDirection.Left, false);
            });
        }
    }

}
