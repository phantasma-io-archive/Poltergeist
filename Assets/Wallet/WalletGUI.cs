using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Poltergeist
{
    public enum WalletState
    {
        Invalid,
        Accounts,
        Balances
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

    public class WalletGUI : MonoBehaviour
    {
        public GUISkin guiSkin;

        private Rect windowRect = new Rect(0, 0, 600, 400);
        private Rect defaultRect;

        private Rect modalRect;

        private WalletState currentState;
        private Stack<WalletState> stateStack = new Stack<WalletState>();

        private int selectedAccountIndex;
        private Action<bool> passwordPromptCallback;
        private string accountPasswordInput;
        private PromptResult passwordPromptResult;

        private Balance[] currentBalances;

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
            windowRect.width = Mathf.Min(600, Screen.width) - border;
            windowRect.height = Mathf.Min(800, Screen.height) - border;

            windowRect.x = (Screen.width - windowRect.width) / 2;
            windowRect.y = (Screen.height - windowRect.height) / 2;

            defaultRect = new Rect(windowRect);

            PushState(WalletState.Accounts);
        }

        #region UTILS
        private void PushState(WalletState state)
        {
            if (currentState != WalletState.Invalid)
            {
                stateStack.Push(currentState);
            }
            currentState = state;
        }

        private void PopState()
        {
            currentState = stateStack.Pop();
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
            }

            passwordPromptResult = PromptResult.Waiting;
            accountPasswordInput = "";
            passwordPromptCallback = callback;
        }
#endregion

        private void Update()
        {
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

            switch (currentState)
            {
                case WalletState.Accounts:
                    DoAccountScreen();
                    break;

                case WalletState.Balances:
                    DoBalanceScreen();
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
                if (GUI.Button(new Rect(windowRect.width - (btnWidth + Units(2)), curY + Units(2), btnWidth, Units(2)), "Select"))
                {
                    selectedAccountIndex = i;
                    RequestPassword((sucess) =>
                    {
                        if (sucess)
                        {
                            var selectedAccount = accountManager.Accounts[selectedAccountIndex];
                            accountManager.FetchBalances(selectedAccount, (balances) =>
                            {
                                this.currentBalances = balances;
                                PushState(WalletState.Balances);

                                Animate(AnimationDirection.Up, false);
                            });
                        }
                        else
                        {
                            selectedAccountIndex = -1;
                        }
                    });
                }

                curY += Units(1);
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

        private void DoBalanceScreen()
        {
            int curY = Units(5);

            var accountManager = AccountManager.Instance;
            var selectedAccount = accountManager.Accounts[selectedAccountIndex];

            int headerSize = Units(10);
            GUI.Label(new Rect((windowRect.width - headerSize)/2, curY, headerSize, Units(2)), "BALANCES");
            curY += Units(1);

            for (int i = 0; i < currentBalances.Length; i++)
            {
                var balance = currentBalances[i];

                var icon = ResourceManager.Instance.GetToken(balance.symbol);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(Units(2), curY + Units(1), Units(2), Units(2)), icon);
                }

                var panelHeight = Units(8);
                var rect = GetExpandedRect(curY, panelHeight);
                GUI.Box(rect, "");

                int btnWidth = Units(11);
                int halfWidth = (int)(rect.width / 2);

                GUI.Label(new Rect(Units(5), curY + Units(1), Units(10), Units(2)), $"{balance.amount} {balance.symbol}");

                curY += Units(2);
            }

            DoBottomBar();
        }

        private void DoBottomBar()
        {

            var panelHeight = Units(9);
            int curY = (int)(windowRect.height - panelHeight + Units(1));

            var rect = GetExpandedRect(curY, panelHeight);
            GUI.Box(rect, "");

            int btnWidth = Units(11);
            int halfWidth = (int)(rect.width / 2);

            GUI.Label(new Rect(halfWidth - 10, curY + Units(3), 28, 20), "or");

            //GUI.Button(new Rect((halfWidth - btnWidth) / 2, prevY + Units(3), btnWidth, Units(2)), "Something");

            if (GUI.Button(new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY + Units(3), btnWidth, Units(2)), "Logout"))
            {
                Animate(AnimationDirection.Right, true, () =>
                {
                    selectedAccountIndex = -1;
                    currentBalances = null;
                    stateStack.Clear();
                    PushState(WalletState.Accounts);
                    Animate(AnimationDirection.Left, false);
                });
            }
        }

    }

}
