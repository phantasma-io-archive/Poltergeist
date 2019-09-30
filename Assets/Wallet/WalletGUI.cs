using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Phantasma.VM.Utils;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Contracts;
using Phantasma.Numerics;

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

    public enum WalletState
    {
        Refreshing,
        Ready,
        Error
    }

    public class WalletGUI : MonoBehaviour
    {
        public GUISkin guiSkin;

        private Rect windowRect = new Rect(0, 0, 600, 400);
        private Rect defaultRect;

        private Rect modalRect;

        private GUIState guiState;
        private Stack<GUIState> stateStack = new Stack<GUIState>();

        private int selectedAccountIndex;
        private Action<bool> passwordPromptCallback;
        private string accountPasswordInput;
        private PromptResult passwordPromptResult;

        private AccountState accountState;
        private WalletState walletState;

        private string transferSymbol;
        private Hash transactionHash;

        private AnimationDirection currentAnimation;
        private float animationTime;
        private bool invertAnimation;
        private Action animationCallback;

        private bool HasAnimation => currentAnimation != AnimationDirection.None;

        private int Units(int n)
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

            guiState = GUIState.Loading;;
        }

#region UTILS
        private void PushState(GUIState state)
        {
            if (guiState != GUIState.Loading)
            {
                stateStack.Push(guiState);
            }
            guiState = state;
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

        public void RequestPassword(Action<bool> callback)
        {
            if (selectedAccountIndex == -1)
            {
                callback(false);
                return;
            }

            if (string.IsNullOrEmpty(AccountManager.Instance.Accounts[selectedAccountIndex].password))
            {
                callback(true);
                return;
            }

            passwordPromptResult = PromptResult.Waiting;
            accountPasswordInput = "";
            passwordPromptCallback = callback;
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

            if (passwordPromptResult != PromptResult.Waiting)
            {
                var temp = passwordPromptCallback;
                var success = passwordPromptResult == PromptResult.Success;
                passwordPromptCallback = null;
                passwordPromptResult = PromptResult.Waiting;
                temp?.Invoke(success);
            }
        }

        void OnGUI()
        {
            GUI.skin = guiSkin;
            GUI.Window(0, windowRect, DoMainWindow, "Poltergeist Wallet");

            if (passwordPromptCallback != null)
            {
                var modalWidth = Units(30);
                var modalHeight = Units(20);
                modalRect = new Rect((Screen.width - modalWidth) / 2, (Screen.height - modalHeight) / 2, modalWidth, modalHeight);
                modalRect = GUI.ModalWindow(0, modalRect, DoPasswordWindow, "Account Authorization");
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
                    DrawCenteredText(AccountManager.Instance.Status);
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

                case GUIState.Balances:
                    DoBalanceScreen();
                    break;

                case GUIState.Transfer:
                    DoTransferScreen();
                    break;
            }

            //GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }

        private void DoPasswordWindow(int windowID)
        {
            var accountManager = AccountManager.Instance;
            var selectedAccount = accountManager.Accounts[this.selectedAccountIndex];

            int curY = Units(4);

            var rect = new Rect(Units(1), curY, modalRect.width - Units(2), modalRect.height - Units(2));

            GUI.Label(new Rect(rect.x, curY, rect.width, Units(2)), "Account: "+selectedAccount.ToString());
            curY += Units(2);

            GUI.Label(new Rect(rect.x, curY, rect.width, Units(2)), "Insert password to proceed");
            curY += Units(3);

            accountPasswordInput = GUI.PasswordField(new Rect(rect.x, curY, rect.width, Units(2)), accountPasswordInput, '*', Account.MaxPasswordLength);

            int btnWidth = Units(11);
            int halfWidth = (int)(rect.width / 2);

            curY = (int)(rect.height - Units(2));
            if (GUI.Button(new Rect((halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), "Cancel"))
            {
                passwordPromptResult = PromptResult.Failure;
            }

            if (GUI.Button(new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), "Confirm"))
            {
                passwordPromptResult = (accountPasswordInput == selectedAccount.password) ? PromptResult.Success : PromptResult.Failure;
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

                GUI.Label(new Rect(Units(2), curY + Units(1), Units(10), Units(2)), account.ToString());

                GUI.enabled = accountManager.IsPlatformEnabled(account.platform);
                if (GUI.Button(new Rect(windowRect.width - (btnWidth + Units(2)), curY + Units(2), btnWidth, Units(2)), "Open"))
                {
                    selectedAccountIndex = i;
                    RequestPassword((sucess) =>
                    {
                        if (sucess)
                        {
                            var selectedAccount = accountManager.Accounts[selectedAccountIndex];

                            walletState = WalletState.Refreshing;
                            Animate(AnimationDirection.Down, true, () => {
                                PushState(GUIState.Balances);

                                StartCoroutine(accountManager.FetchBalances(selectedAccount, (state) =>
                                {
                                    if (state != null)
                                    {
                                        this.accountState = state;
                                        walletState = WalletState.Ready;
                                    }
                                    else
                                    {
                                        walletState = WalletState.Error;
                                    }
                                }));

                                Animate(AnimationDirection.Up, false);
                            });


                        }
                        else
                        {
                            selectedAccountIndex = -1;
                        }
                    });
                }
                GUI.enabled = true;

                curY += Units(6);
            }

            // import account panel on bottom
            {
                var panelHeight = Units(9);
                curY = (int)(windowRect.height - panelHeight + Units(1));
                var rect = GetExpandedRect(curY, panelHeight);
                GUI.Box(rect, "Add account");

                int btnWidth = Units(11);
                int halfWidth = (int)(rect.width / 2);

                GUI.Label(new Rect(halfWidth - 10, curY + Units(3), 28, 20), "or");

                GUI.Button(new Rect((halfWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Generate new wallet");
                GUI.Button(new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Import private key");
            }
        }

        private void DrawCenteredText(string caption)
        {
            var style = GUI.skin.label;
            var content = new GUIContent(caption);
            var size = new Vector2(200, 40); //   style.CalcSize(content);
            GUI.Label(new Rect((windowRect.width - size.x) / 2, (windowRect.height - size.y) / 2, size.x, size.y), content);
        }

        private void DoCloseButton()
        {
            if (GUI.Button(new Rect(windowRect.width - Units(3), Units(1), Units(2), Units(2)), "X"))
            {
                Animate(AnimationDirection.Right, true, () =>
                {
                    selectedAccountIndex = -1;
                    accountState = null;
                    stateStack.Clear();
                    PushState(GUIState.Accounts);
                    Animate(AnimationDirection.Left, false);
                });
            }
        }

        private void DoBalanceScreen()
        {
            switch (walletState)
            {
                case WalletState.Refreshing:
                    DrawCenteredText("Fetching balances...");
                    return;

                case WalletState.Error:
                    DrawCenteredText("Error fetching balances...");
                    DoCloseButton();
                    return;
            }

            DoCloseButton();

            int curY = Units(4);

            int headerSize = Units(10);
            GUI.Label(new Rect((windowRect.width - headerSize) / 2, curY, headerSize, Units(2)), "BALANCES");
            curY += Units(3);

            var accountManager = AccountManager.Instance;
            var selectedAccount = accountManager.Accounts[selectedAccountIndex];

            Rect rect;
            int panelHeight;

            decimal feeBalance = 0;

            foreach (var balance in accountState.balances)
            {
                if (balance.Symbol == "KCAL")
                {
                    feeBalance += balance.Amount;
                }
            }

            int btnWidth;
            int i = 0;
            foreach (var balance in accountState.balances)
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

                GUI.Label(new Rect(Units(5), curY + Units(1), Units(20), Units(2)), $"{balance.Amount} {balance.Symbol}");

                string secondaryAction;
                bool secondaryEnabled;
                Action secondaryCallback;

                switch (balance.Symbol)
                {
                    case "SOUL":
                        secondaryAction = "Stake";
                        secondaryEnabled = this.accountState.stake == 0 && balance.Amount > 0;
                        secondaryCallback = () =>
                        {
                            var address = Address.FromText(this.accountState.address);

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
                        secondaryEnabled = this.accountState.claim > 0;
                        secondaryCallback = () =>
                        {
                            var address = Address.FromText(this.accountState.address);

                            var sb = new ScriptBuilder();
                            sb.AllowGas(address, Address.Null, 1, 9999);
                            sb.CallContract("stake", "Claim", address, address);
                            sb.SpendGas(address);
                            var script = sb.EndScript();

                            SendTransaction(script, "main");
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
                i++;
            }

            if (guiState != GUIState.Balances)
            {
                return;
            }

            panelHeight = Units(9);
            curY = (int)(windowRect.height - panelHeight + Units(1));

            rect = GetExpandedRect(curY, panelHeight);
            GUI.Box(rect, this.accountState.address);

            btnWidth = Units(11);

            int totalWidth = (int)rect.width; // (int)(rect.width / 2);

            //GUI.Button(new Rect((halfWidth - btnWidth) / 2, prevY + Units(3), btnWidth, Units(2)), "Something");

            int leftoverWidth = (int)(rect.width - totalWidth);

            if (GUI.Button(new Rect(leftoverWidth + (totalWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Copy Address"))
            {
                EditorGUIUtility.systemCopyBuffer = this.accountState.address;
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
            var selectedAccount = accountManager.Accounts[selectedAccountIndex];

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
            var selectedAccount = accountManager.Accounts[this.selectedAccountIndex];

            Animate(AnimationDirection.Right, true, () =>
            {
                PushState(GUIState.Sending);
                accountManager.SignAndSendTransaction(selectedAccount, "chain", script, (hash) =>
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
