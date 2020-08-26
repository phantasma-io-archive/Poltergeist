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
using Phantasma.SDK;

using ZXing;
using ZXing.QrCode;
using System.Globalization;
using Phantasma.Core.Types;
using Tetrochain;
using System.Collections;
using Phantasma.Ethereum;
using System.Threading;
using Phantasma.Neo.Core;

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
        TtrsNft, // Full list of TTRS NFTs with sorting and filtering capabilities.
        TtrsNftView, // Full list of TTRS NFTs with sorting and filtering capabilities, view only mode.
        TtrsNftTransferList, // List of user-selected TTRS NFTs, ready to be transfered to another wallet.
        History,
        Account,
        Sending,
        Confirming,
        Settings,
        ScanQR,
        Backup,
        Dapps,
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

    public enum TtrsNftSortMode // NFT-specific, TTRS-specific. Modes of NFT list sorting.
    {
        None,
        Number_Date,
        Date_Number,
        Type_Number_Date,
        Type_Date_Number,
        Type_Rarity
    }

    public enum SortDirection // Direction of sorting, used in NFT list sorting.
    {
        None,
        Ascending,
        Descending
    }

    public enum ttrsNftType // NFT-specific, TTRS-specific. Types of TTRS NFTs.
    {
        All,
        Vehicle,
        Part,
        License
    }

    public enum ttrsNftRarity // NFT-specific, TTRS-specific. Rarity classes of TTRS NFTs.
    {
        All = 0,
        Consumer = 1,
        Industrial = 2,
        Professional = 3,
        Collector = 4
    }

    public enum nftMinted // NFT-specific. Used in NFT filter, allows to select NFTs by mint date. All intervals are 'rolling'.
    {
        All,
        Last_15_Mins,
        Last_Hour,
        Last_24_Hours,
        Last_Week,
        Last_Month
    }

    public class WalletGUI : MonoBehaviour, IWalletConnector
    {
        public RawImage background;
        private Texture2D soulMasterLogo;

        private Dictionary<PlatformKind, Texture2D> QRCodeTextures = new Dictionary<PlatformKind, Texture2D>();

        public const string WalletTitle = "Poltergeist Wallet";
        public const string MoneyFormat = "#,0.####";
        public const string MoneyFormatShort = "#,0.##";
        public const string MoneyFormatLong = "#,0.############";

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

        private int logLevelIndex;
        private ComboBox logLevelComboBox = new ComboBox();

        private int uiThemeIndex;
        private ComboBox uiThemeComboBox = new ComboBox();

        // NFT sorting and filtering.
        private ComboBox nftSortModeComboBox = new ComboBox();
        private string nftFilterName;
        private ComboBox nftTypeComboBox = new ComboBox();
        private int nftFilterTypeIndex = 0;
        private string nftFilterType = "All";
        private ComboBox nftRarityComboBox = new ComboBox();
        private int nftFilterRarity = 0;
        private ComboBox nftMintedComboBox = new ComboBox();
        private int nftFilterMinted = 0;

        // NFT pagination.
        private int nftPageSize = 25;
        private int nftPageNumber = 0;
        private int nftCount = 0;
        private int nftPageCount = 0;
        private List<string> nftFilteredList = new List<string>(); // List of displayed NFT items (after applying filters).
        private List<string> nftTransferList = new List<string>(); // List of NFT items, selected by user.

        private Log.Level[] availableLogLevels = Enum.GetValues(typeof(Log.Level)).Cast<Log.Level>().ToArray();

        private UiThemes[] availableUiThemes = Enum.GetValues(typeof(UiThemes)).Cast<UiThemes>().ToArray();

        private bool initialized;

        private int virtualWidth;
        private int virtualHeight;

        private string fatalError;

        public static WalletGUI Instance { get; private set; }

        public static int Units(int n)
        {
            return 16 * n;
        }

        private void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            // Getting wallet's command line args.
            string[] _args = System.Environment.GetCommandLineArgs();

            // We have to get these settings prior to Settings.Load() call,
            // to initialize log properly.
            AccountManager.Instance.Settings.LoadLogSettings();

            Log.Level _logLevel = AccountManager.Instance.Settings.logLevel;
            var _logOverwriteMode = AccountManager.Instance.Settings.logOverwriteMode;
            bool _logForceWorkingFolderUsage = false;

            // Checking if log options are set in command line.
            // They override settings (for debug purposes).
            for (int i = 0; i < _args.Length; i++)
            {
                switch (_args[i])
                {
                    case "--log-level":
                        {
                            if (i + 1 < _args.Length)
                            {
                                Enum.TryParse<Log.Level>(_args[i + 1], true, out _logLevel);
                            }

                            break;
                        }

                    case "--log-force-working-folder-usage":
                        {
                            _logForceWorkingFolderUsage = true;

                            break;
                        }
                }
            }

            Log.Init("poltergeist.log", _logLevel, _logForceWorkingFolderUsage, _logOverwriteMode);
            Log.Write("********************************************************\n" +
                       "************** Poltergeist Wallet started **************\n" +
                       "********************************************************\n" +
                       "Wallet version: " + UnityEngine.Application.version + "\n" +
                       "Log level: " + _logLevel.ToString());

            Cache.Init("cache");

            initialized = false;

            guiState = GUIState.Loading;

            Log.Write(Screen.width + " x " + Screen.height);
            currencyOptions = AccountManager.Instance.Currencies.ToArray();

            StartCoroutine(TtrsStore.LoadStoreInfo());

            // We will use this RawImage object to set/change background image.
            background = GameObject.Find("Background").GetComponent<RawImage>();
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
                Log.Write($"Fatal error: {fatalError}");
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

                case GUIState.Dapps:
                    EmulatorManager.Instance.UnloadROM();
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

                case GUIState.Dapps:
                    currentTitle = "Dapps";
                    EmulatorManager.Instance.LoadROM();
                    break;

                case GUIState.Wallets:
                    currentTitle = "Wallet List";

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

                case GUIState.TtrsNft:
                case GUIState.TtrsNftView:
                    currentTitle = "TTRS NFTs for " + accountManager.CurrentAccount.name;
                    transferSymbol = "TTRS";
                    accountManager.ResetTtrsNftsSorting();
                    break;

                case GUIState.TtrsNftTransferList:
                    currentTitle = "TTRS NFTs transfer list for " + accountManager.CurrentAccount.name;
                    nftTransferListScroll = Vector2.zero;
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
                        settingsScroll = Vector2.zero;
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

                        logLevelIndex = 0;
                        for (int i = 0; i < availableLogLevels.Length; i++)
                        {
                            if (availableLogLevels[i] == accountManager.Settings.logLevel)
                            {
                                logLevelIndex = i;
                                break;
                            }
                        }
                        logLevelComboBox.SelectedItemIndex = logLevelIndex;

                        uiThemeIndex = 0;
                        for (int i = 0; i < availableUiThemes.Length; i++)
                        {
                            if (availableUiThemes[i].ToString() == accountManager.Settings.uiThemeName)
                            {
                                uiThemeIndex = i;
                                break;
                            }
                        }
                        uiThemeComboBox.SelectedItemIndex = uiThemeIndex;

                        

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
        private string[] ModalOkCopy = new string[] { "Ok", "Copy to clipboard" };
        private string[] ModalOkView = new string[] { "Ok", "View" };
        private string[] ModalConfirmCancel = new string[] { "Confirm", "Cancel" };
        private string[] ModalSendCancel = new string[] { "Send", "Cancel" };
        private string[] ModalYesNo = new string[] { "Yes" , "No" };
        private string[] ModalHexWif = new string[] { "HEX format", "WIF format" };

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

        private void ShowModal(string title, string caption, ModalState state, int minInputLength, int maxInputLength, string[] options, int multiLine, Action<PromptResult, string> callback, int confirmDelay = 0)
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
                callback(result);
            }, confirmDelay);
        }

        public void MessageBox(MessageKind kind, string caption, Action callback = null)
        {
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

        #region CONNECTOR PROMPT
        
        private string _promptText;
        private Action<bool> _promptCallback;
        private bool _promptVisible;

        public void Prompt(string text, Action<bool> callback)
        {
            // if theres an active prompt, this new one automatically fails
            if (_promptText != null)
            {
                callback(false);
                return;
            }

            _promptText = text;
            _promptCallback = callback;
            _promptVisible = false;
        }

        private void UpdatePrompt()
        {
            if (_promptText == null || _promptVisible)
            {
                return;
            }

            _promptVisible = true;

            PromptBox(_promptText, ModalYesNo, (result) =>
            {
                var temp = _promptCallback;
                _promptText = null;
                temp(result == PromptResult.Success);
            });
        }
        #endregion

        // This code is needed for Android to quit wallet on 'Back' double press.
        int escClickCounter = 0;
        IEnumerator escClickTime()
        {
            yield return new WaitForSeconds(0.5f);
            escClickCounter = 0;
        }
        private void Update()
        {
            /*if (Input.GetKeyDown(KeyCode.Z))
            {
                AccountState state = null;
                state.address += "";
            }*/

            // This code is needed for Android to quit wallet on 'Back' double press.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escClickCounter++;
                StartCoroutine(escClickTime());

                if (escClickCounter > 1 && Application.platform == RuntimePlatform.Android)
                {
                    Application.Quit();
                }
            }

            UpdatePrompt();

            lock (_uiCallbacks)
            {
                if (_uiCallbacks.Count > 0)
                {
                    Action[] temp;
                    lock (_uiCallbacks)
                    {
                        temp = _uiCallbacks.ToArray();
                    }
                    _uiCallbacks.Clear();

                    foreach (var callback in temp)
                    {
                        callback.Invoke();
                    }
                }
            }

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

            var uiThemeName = AccountManager.Instance.Settings.uiThemeName;
            if (uiThemeName == UiThemes.Classic.ToString())
            {
                GUI.skin = guiSkin;
            }
            else
            {
                GUI.skin = Resources.Load($"Skins/{uiThemeName}/{uiThemeName}") as GUISkin;
            }

            if (VerticalLayout)
                background.texture = Resources.Load<Texture2D>($"Skins/{uiThemeName}/mobile_background");
            else
                background.texture = Resources.Load<Texture2D>($"Skins/{uiThemeName}/background");
            soulMasterLogo = Resources.Load<Texture2D>($"Skins/{AccountManager.Instance.Settings.uiThemeName}/soul_master");

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

        void OnApplicationQuit()
        {
            AccountManager.Instance.Settings.SaveOnExit();
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
            style.fontSize -= 6;
            GUI.Label(new Rect(windowRect.width / 2 + ((AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString()) ? Units(7) - 4 : Units(5)), 12, 32, Units(2)), Application.version);
            style.fontSize += 6;

            var accountManager = AccountManager.Instance;

            if (currentTitle != null && this.currentAnimation == AnimationDirection.None && !accountManager.Refreshing)
            {
                int curY = Units(3);

                var tempTitle = currentTitle;

                switch (guiState)
                {
                    case GUIState.TtrsNft:
                    case GUIState.TtrsNftView:
                        if (nftTransferList.Count > 0)
                            tempTitle = $"{nftCount} ({nftTransferList.Count} selected) {tempTitle}";
                        else
                            tempTitle = $"{nftCount} {tempTitle}";
                        break;
                    case GUIState.TtrsNftTransferList:
                        tempTitle = $"{nftTransferList.Count} {tempTitle}";
                        break;
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

                // Drawing build timestamp at the Settings screen
                if (guiState == GUIState.Settings)
                {
                    style = GUI.skin.label;
                    style.fontSize -= 6;
                    var temp = style.alignment;
                    style.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(new Rect(0, (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString()) ? curY + Units(1) - 4 : curY + Units(1), windowRect.width, Units(2)), $"Version was built on: {Poltergeist.Build.Info.Instance.BuildTime} UTC");
                    style.alignment = temp;
                    style.fontSize += 6;
                }
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

                case GUIState.TtrsNft:
                case GUIState.TtrsNftView:
                    DoTtrsNftScreen();
                    break;

                case GUIState.TtrsNftTransferList:
                    DoTtrsNftTransferListScreen();
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

                case GUIState.Dapps:
                    DoDappScreen();
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
            int captionDisplayedHeight =  Math.Min(captionAvailableHeight, captionHeight);

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

                DoButton(true, new Rect((halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), modalOptions[1], () =>
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

                DoButton(Time.time - modalTime >= modalConfirmDelay && (modalState != ModalState.Input || modalInput.Length >= modalMinInputLength),
                    new Rect(halfWidth + (halfWidth - btnWidth) / 2, curY, btnWidth, Units(2)), (modalConfirmDelay > 0 && (Time.time - modalTime < modalConfirmDelay)) ? modalOptions[0] + " (" + (modalConfirmDelay - Math.Floor(Time.time - modalTime)) + ")" : modalOptions[0], () =>
                {
                    AudioManager.Instance.PlaySFX("confirm");
                    modalResult = PromptResult.Success;
                });
            }
            else
            if (modalOptions.Length > 0)
            {
                DoButton(true, new Rect((modalRect.width - btnWidth) / 2, curY, btnWidth, Units(2)), modalOptions[0], () =>
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
            Log.Write("Login into account initiated.");

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

            ShowModal("Wallet Name", "Enter a name for your wallet", ModalState.Input, 3, 16, ModalConfirmCancel, 1, (result, name) =>
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

                var platforms = PlatformKind.None;
                foreach (var platform in AccountManager.AvailablePlatforms)
                {
                    platforms |= platform;
                }

                int walletIndex = accountManager.AddWallet(name, platforms, wif, password);
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
                seedPhrase = null; // seedPhrase is used to determine value of isNewWallet global flag, and should be reset in case of error.
                MessageBox(MessageKind.Error, "Error creating account.\n" + e.Message);
            }
        }

        private string[] accountOptions = new string[] { "Generate new wallet", "Import wallet", "Settings" };

        private Vector2 accountScroll;
        private Vector2 balanceScroll;
        private Vector2 nftScroll;
        private Vector2 nftTransferListScroll;
        private Vector2 settingsScroll;

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
                                        ShowModal("Seed import", "Enter seed password.\nIf you put a wrong password, the wrong public address will be generated.\n\nFor wallets created with v1.3 or later, it is ok to leave this blank.\n", ModalState.Input, 0, 64, ModalConfirmCancel, 1, (seedResult, password) =>
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
                        GUI.Label(new Rect(Border * 2, curY , windowRect.width - Border * 2, Units(2) + 4), account.name);
                        GUI.Label(new Rect(Border * 2, curY + Units(1), windowRect.width - Border * 2, Units(2) + 4), $"[{account.platforms}]");
                        btnRect = new Rect((rect.width - btnWidth)/2, curY + Units(3) + 4, btnWidth, Units(2));
                    }
                    else
                    {
                        GUI.Label(new Rect(Border * 2, curY + Units(1), windowRect.width - Border * 2, Units(2) + 4), account.ToString());
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

            int divisionWidth = (int)(windowRect.width / buttonCount);
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
                    btnRect = new Rect(divisionWidth * i + (divisionWidth - btnWidth) / 2, rect.y + border, btnWidth, Units(2));
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

            if (!settings.phantasmaBPURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma RPC URL.\n" + settings.phantasmaBPURL);
                return false;
            }

            if (!settings.neoRPCURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for NEO RPC URL.\n" + settings.neoRPCURL);
                return false;
            }

            if (!settings.neoscanURL.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Neoscan API URL.\n" + settings.neoscanURL);
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

            accountManager.UpdateRPCURL(PlatformKind.Phantasma);
            accountManager.UpdateRPCURL(PlatformKind.Neo);

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

            var labelWidth = Units(10);
            var labelHeight = Units(2) + 4;
            var fieldX = Units(13); // X for fields.
            var fieldComboX = fieldX + 6; // X for combos.
            var fieldWidth = Units(20); // Width of text fields.
            var comboWidth = Units(8); // Width of combo fields.

            int dropHeight;

            // startX, startY: Starting position of "Settings" box.
            int startX = Border;
            int startY = (int)(curY - Border);
            // boxWidth, boxHeight: Size of "Settings" box.
            int boxWidth = (int)(windowRect.width - (Border * 2));
            int boxHeight = (int)(windowRect.height - (Border * 2 + curY));
            
            fieldWidth = Math.Min(fieldWidth, boxWidth - fieldX - Units(3));
            comboWidth = Math.Min(comboWidth, boxWidth - fieldX - Units(3));

            GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "");

            // Height calculation:
            // 1) 19 elements with total height of (element height + spacing) * 19 = Units(3) * 19.
            // 2) Dropdown space for log level combo: Units(2) * 3.
            // 3) Last element has additional Units(1) spacing before it.
            var insideRect = new Rect(0, 0, boxWidth, Units(3) * 19 + Units(2) * 3 + Units(1));
            // Height calculation: Units(4) space in the bottom of box is occupied by buttons row.
            var outsideRect = new Rect(startX, startY, boxWidth, boxHeight - Units(4));

            bool needsScroll = insideRect.height > outsideRect.height;
            if (needsScroll)
            {
                insideRect.width -= Border;
            }

            settingsScroll = GUI.BeginScrollView(outsideRect, settingsScroll, insideRect);
            
            var posX = Units(3);

            curY = Units(1); // Vertical position inside scroll view.

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Currency");
            currencyIndex = currencyComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), currencyOptions, 0, out dropHeight);
            settings.currency = currencyOptions[currencyIndex];
            curY += dropHeight + Units(1);

            settings.sfx = GUI.Toggle(new Rect(posX, curY, Units(2), Units(2)), settings.sfx, "");
            GUI.Label(new Rect(posX + Units(2), curY, Units(9), labelHeight), "Sound Effects");
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Nexus");
            var nexusList = availableNexus.Select(x => x.ToString().Replace('_', ' ')).ToArray();
            var prevNexus = nexusIndex;
            nexusIndex = nexusComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), nexusList, 0, out dropHeight, null, 1);
            settings.nexusKind = availableNexus[nexusIndex];
            curY += dropHeight + Units(1);

            if (settings.nexusKind != NexusKind.Main_Net && settings.nexusKind != NexusKind.Custom && settings.nexusKind != NexusKind.Unknown)
            {
                var style = GUI.skin.label;
                var tempStyle = style.fontStyle;
                style.fontStyle = FontStyle.Italic;
                var warningHeight = Units(VerticalLayout ? 6: 4);
                GUI.Label(new Rect(posX, curY, boxWidth - (posX + Border*2), warningHeight), "WARNING - Use this network only if you are a developer or tester.\nAll assets used here are only for development, not real.");
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
                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Phantasma RPC URL");
                settings.phantasmaBPURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.phantasmaBPURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Neo RPC URL");
                settings.neoRPCURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.neoRPCURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Neoscan API URL");
                settings.neoscanURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.neoscanURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Ethereum RPC URL");
                settings.ethereumRPCURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.ethereumRPCURL);
                curY += Units(3);
            }
            else
            {
                settings.RestoreEndpoints(!hasCustomName);
            }

            if (hasCustomName)
            {
                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Nexus Name");
                settings.nexusName = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.nexusName);
                curY += Units(3);
            }

            if (hasCustomFee)
            {
                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Phantasma fee price");
                var fee = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.feePrice.ToString());
                BigInteger.TryParse(fee, out settings.feePrice);
                curY += Units(3);
            }

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Neo GAS fee");
            var neoGasFee = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.neoGasFee.ToString());
            neoGasFee = neoGasFee.EndsWith(".") || neoGasFee.EndsWith(",") ? neoGasFee + "0" : neoGasFee;
            Decimal.TryParse(neoGasFee, out settings.neoGasFee);
            curY += Units(3);

            // Ethereum fees, should be editable in all modes.

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Eth gas price (Gwei)");
            var ethereumGasPriceGwei = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.ethereumGasPriceGwei.ToString());
            BigInteger.TryParse(ethereumGasPriceGwei, out settings.ethereumGasPriceGwei);
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Eth transfer gas limit");
            var ethereumTransactionGasLimit = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.ethereumTransferGasLimit.ToString());
            BigInteger.TryParse(ethereumTransactionGasLimit, out settings.ethereumTransferGasLimit);
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Eth token tr. gas limit");
            var ethereumTokenTransactionGasLimit = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.ethereumTokenTransferGasLimit.ToString());
            BigInteger.TryParse(ethereumTokenTransactionGasLimit, out settings.ethereumTokenTransferGasLimit);
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Log level");
            logLevelIndex = logLevelComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), availableLogLevels.ToArray(), WalletGUI.Units(2) * 3, out dropHeight);
            settings.logLevel = availableLogLevels[logLevelIndex];
            curY += dropHeight + Units(1);

            settings.logOverwriteMode = GUI.Toggle(new Rect(posX, curY, Units(2), Units(2)), settings.logOverwriteMode, "");
            GUI.Label(new Rect(posX + Units(2), curY, Units(9), labelHeight), "Overwrite log");
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "UI theme");
            uiThemeIndex = uiThemeComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), availableUiThemes.ToArray(), WalletGUI.Units(2) * 2, out dropHeight);
            settings.uiThemeName = availableUiThemes[uiThemeIndex].ToString();
            curY += dropHeight + Units(1);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Clear cache", () =>
            {
                PromptBox("Are you sure you want to clear wallet's cache?", ModalConfirmCancel, (result) =>
                {
                    if (result == PromptResult.Success)
                    {
                        AudioManager.Instance.PlaySFX("click");
                        Cache.Clear();
                        MessageBox(MessageKind.Default, "Cache cleared.");
                    }
                });
            });
            curY += Units(3);

            if (accountManager.Accounts.Length > 0)
            {
                curY += Units(1);
                DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Delete Everything", () =>
                {
                    PromptBox("All wallets and settings stored in this device will be lost.\nMake sure you have backups of your private keys!\nOtherwise you will lose access to your funds.", ModalConfirmCancel, (result) =>
                    {
                        if (result == PromptResult.Success)
                        {
                            AudioManager.Instance.PlaySFX("click");
                            accountManager.DeleteAll();
                            PlayerPrefs.DeleteAll();
                            accountManager.Settings.Load();
                            MessageBox(MessageKind.Default, "All data removed from this device.", () =>
                            {
                                CloseCurrentStack();
                            });
                        }
                    }, 15);
                });

                curY += Units(3);
            }
            
            GUI.EndScrollView();

            var btnWidth = Units(10);
            var btnHeight = Units(2);
            var btnVerticalSpacing = 4;
            curY = (int)(windowRect.height - Units(6));

            Rect cancelBtnRect;
            Rect confirmBtnRect;

            if (VerticalLayout)
            {
                cancelBtnRect = new Rect(startX + Border * 2, startY + boxHeight - btnHeight - Border, boxWidth - Border * 4, btnHeight);
                confirmBtnRect = new Rect(startX + Border * 2, startY + boxHeight - btnHeight * 2 - Border - btnVerticalSpacing, boxWidth - Border * 4, btnHeight);
            }
            else
            {
                cancelBtnRect = new Rect(windowRect.width / 3 - btnWidth / 2, curY, btnWidth, btnHeight);
                confirmBtnRect = new Rect((windowRect.width / 3) * 2 - btnWidth / 2, curY, btnWidth, btnHeight);
            }

            DoButton(true, cancelBtnRect, "Cancel", () =>
            {
                AudioManager.Instance.PlaySFX("cancel");

                // Resetting changes by restoring current settings.
                settings.Load();

                // Restoring combos' selected items.
                // If they are not restored, following calls of DoSettingsScreen() will change them again.
                SetState(GUIState.Settings);

                CloseCurrentStack();
            });
            DoButton(true, confirmBtnRect, "Confirm", () =>
            {
                if (ValidateSettings())
                {
                    AudioManager.Instance.PlaySFX("confirm");
                    ResourceManager.Instance.UnloadTokens();
                    CloseCurrentStack();
                }
            });
        }

        private int DrawPlatformTopMenu(Action refresh, bool showCopyToClipboardButton = true)
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

                case PlatformKind.Ethereum:
                    mainToken = "ETH";
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

            // Saving platform combo position to draw it later.
            int platformComboBoxY = curY;

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

            if (showCopyToClipboardButton)
            {
                DoButton(true, new Rect((windowRect.width - btnWidth) / 2, curY, btnWidth, Units(1)), "Copy Address", () =>
                  {
                      AudioManager.Instance.PlaySFX("click");
                      GUIUtility.systemCopyBuffer = state.address;
                      MessageBox(MessageKind.Default, "Address copied to clipboard.");
                  });

                curY += Units(3);
            }

            // Drawing combo in the very end, to avoid combo dropdown overlapping with other elements.
            int currentPlatformIndex = 0;
            var platformList = accountManager.CurrentAccount.platforms.Split();

            // We do not show platform combo for NFTs screens to avoid errors.
            if (platformList.Count > 1 && (guiState != GUIState.TtrsNft && guiState != GUIState.TtrsNftView && guiState != GUIState.TtrsNftTransferList))
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
                var platformIndex = platformComboBox.Show(new Rect(Units(4) + 8, platformComboBoxY, Units(8), Units(1)), platformList, 0, out dropHeight);

                if (platformIndex != currentPlatformIndex)
                {
                    accountManager.CurrentPlatform = platformList[platformIndex];
                }
            }

            return curY;
        }

        // NFT tools for toolbar over NFT list - sort/filters combos, select/invert buttons etc.
        private void DrawNftTools( int posY )
        {
            var accountManager = AccountManager.Instance;

            if (transferSymbol == "TTRS")
            {
                var posX1 = Units(2);
                var posX2 = posX1 + toolLabelWidth + toolFieldWidth + toolFieldSpacing;
                // 2nd row of widgets for VerticalLayout
                var posX3 = (VerticalLayout) ? Units(2) : posX2 + toolLabelWidth + toolFieldWidth + toolFieldSpacing;
                var posX4 = posX3 + toolLabelWidth + toolFieldWidth + toolFieldSpacing;
                var posY2 = (VerticalLayout) ? posY + Units(2) : posY;
                var posY3 = (VerticalLayout) ? posY2 + Units(2) : posY + Units(2);

                // #5: Sorting mode combo
                DoNftToolComboBox(posX1, posY3, nftSortModeComboBox, Enum.GetValues(typeof(TtrsNftSortMode)).Cast<TtrsNftSortMode>().ToList().Select(x => x.ToString().Replace("_", ", ").Replace("Number", "#")).ToList(), "Sort: ", ref accountManager.Settings.ttrsNftSortMode);

                // #6: Sorting direction button
                DoNftToolButton(posX2 + 4,
                                posY3,
                                (VerticalLayout) ? toolLabelWidth - toolFieldSpacing - 8 : toolLabelWidth - toolFieldSpacing, (accountManager.Settings.nftSortDirection == (int)SortDirection.Ascending) ? "Asc" : "Desc", () => { if (accountManager.Settings.nftSortDirection == (int)SortDirection.Ascending) accountManager.Settings.nftSortDirection = (int)SortDirection.Descending; else accountManager.Settings.nftSortDirection = (int)SortDirection.Ascending; });

                if (guiState != GUIState.TtrsNftView)
                {
                    // #7: Select all button
                    DoNftToolButton(posX4 + toolLabelWidth,
                                posY3,
                                (VerticalLayout) ? toolLabelWidth - toolFieldSpacing - 8 : toolLabelWidth - toolFieldSpacing, "Select", () =>
                                {
                                    if (nftFilteredList.Count > 0)
                                    {
                                        // If filter is applied, select button selects only filtered items.
                                        nftFilteredList.ForEach((x) => { if (!nftTransferList.Contains(x)) nftTransferList.Add(x); });
                                    }
                                    else
                                    {
                                        // If no filter is applied, select button selects all items.
                                        nftTransferList.Clear();
                                        accountManager.CurrentNfts.ForEach((x) => { nftTransferList.Add(x); });
                                    }
                                });

                    // #8: Invert selection button
                    DoNftToolButton((VerticalLayout) ? posX4 + toolLabelWidth * 2 - toolFieldSpacing + 8 : posX4 + toolLabelWidth * 2 + toolFieldSpacing,
                                    posY3,
                                    (VerticalLayout) ? toolLabelWidth - toolFieldSpacing - 8 : toolLabelWidth - toolFieldSpacing, "Invert", () =>
                                    {
                                        if (nftFilteredList.Count > 0)
                                        {
                                        // If filter is applied, invert button processes only filtered items.
                                        nftFilteredList.ForEach((x) => { if (!nftTransferList.Contains(x)) nftTransferList.Add(x); else nftTransferList.Remove(x); });
                                        }
                                        else
                                        {
                                        // If no filter is applied, invert button processes all items.
                                        var nftTransferListCopy = new List<string>();
                                            accountManager.CurrentNfts.ForEach((x) => { if (!nftTransferList.Exists(y => y == x)) { nftTransferListCopy.Add(x); } });
                                            nftTransferList = nftTransferListCopy;
                                        }
                                    });
                }

                // #3: NFT rarity filter
                DoNftToolComboBox(posX3, posY2, nftRarityComboBox, Enum.GetValues(typeof(ttrsNftRarity)).Cast<ttrsNftRarity>().ToList(), "Rarity: ", ref nftFilterRarity);

                // #4: NFT mint date filter
                DoNftToolComboBox(posX4, posY2, nftMintedComboBox, Enum.GetValues(typeof(nftMinted)).Cast<nftMinted>().ToList().Select(x => x.ToString().Replace('_', ' ')).ToList(), "Minted: ", ref nftFilterMinted);

                // #1: NFT name filter
                DoNftToolTextField(posX1, posY, "Name: ", ref nftFilterName);

                // #2: NFT type filter
                DoNftToolComboBox(posX2, posY, nftTypeComboBox, Enum.GetValues(typeof(ttrsNftType)).Cast<ttrsNftType>().ToList(), "Type: ", ref nftFilterTypeIndex);
                if (Enum.IsDefined(typeof(ttrsNftType), nftFilterTypeIndex))
                    nftFilterType = ((ttrsNftType)nftFilterTypeIndex).ToString();
                else
                    nftFilterType = "All";
            }
        }

        private bool DrawNftToolsAreActive()
        {
            return nftTypeComboBox.DropDownIsOpened() || nftMintedComboBox.DropDownIsOpened() || nftRarityComboBox.DropDownIsOpened();
        }

        private void DrawBalanceLine(ref Rect subRect, string symbol, decimal amount, string caption)
        {
            if (amount > 0.0001m)
            {
                var style = GUI.skin.label;
                var tempColor = style.normal.textColor;
                style.normal.textColor = new Color(1, 1, 1, 0.75f);
                style.fontSize -= VerticalLayout ? 4: 2;

                GUI.Label(subRect, $"{amount.ToString(MoneyFormat)} {symbol} {caption} ({AccountManager.Instance.GetTokenWorth(symbol, amount)})");
                style.fontSize += VerticalLayout ? 4 : 2;
                style.normal.textColor = tempColor;

                // For vertical layout making a height correction proportional to font size difference.
                subRect.y += VerticalLayout ? (int)(Units(1) * (double)16 / 18) + 4 : Units(1) + 4;
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
                DoBackButton();
                return;
            }

            if (WebCamTexture.devices.Count() == 0)
            {
                DrawCenteredText("Camera not found...");
                DoBackButton();
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
                        Log.Write("DECODED TEXT FROM QR: " + result.Text);

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
                catch (Exception ex) { Log.WriteWarning(ex.Message); }
            }

            DoBackButton();
        }

        private void DoDappScreen()
        {
            var accountManager = AccountManager.Instance;

            var dappTexture = EmulatorManager.Instance.screenTexture;

            /*            var dappHeight = windowRect.height - Units(12);
                        var dappWidth = (int)((dappTexture.width * dappHeight) / (float)dappTexture.height);
                        */

            var scale = 2;
            var dappWidth = dappTexture.width * scale;
            var dappHeight = dappTexture.height * scale;

            var camRect = new Rect((windowRect.width - dappWidth) / 2, (windowRect.height - dappHeight) / 2, dappWidth, dappHeight);
            DrawDropshadow(camRect);
            GUI.DrawTexture(camRect, dappTexture, ScaleMode.ScaleToFit);

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
            DoButton(true, new Rect(windowRect.width / 3 - btnWidth / 2, curY, btnWidth, Units(2)), "Copy to clipboard", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                GUIUtility.systemCopyBuffer = seedPhrase;
                MessageBox(MessageKind.Default, "Seed phrase copied to the clipboard.");
            });

            DoButton(true, new Rect((windowRect.width / 3) * 2 - btnWidth / 2, curY, btnWidth, Units(2)), "Continue", () =>
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

            var state = accountManager.CurrentState;

            if (state != null && state.flags.HasFlag(AccountFlags.Master) && soulMasterLogo != null)
            {
                if (VerticalLayout)
                {
                    GUI.DrawTexture(new Rect(windowRect.width - Units(6), Units(4) - 8, Units(6), Units(6)), soulMasterLogo);
                }
                else
                {
                    GUI.DrawTexture(new Rect(Units(1), Units(2) + 8, Units(8), Units(8)), soulMasterLogo);
                }
            }

            var startY = DrawPlatformTopMenu(() =>
            {
                accountManager.RefreshBalances(true);
            });
            var endY = DoBottomMenu();

            if (state == null)
            {
                DrawCenteredText("Temporary error, cannot display balances...");
                return;
            }

            int curY = Units(12);

            decimal feeBalance = state.GetAvailableAmount("KCAL");

            var balanceCount = DoScrollArea<Balance>(ref balanceScroll, startY, endY, VerticalLayout ? Units(7) : Units(6), state.balances.Where(x => x.Total >= 0.001m),
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
                    var iconY = curY;
                    iconY += Units(1); // Adding border height
                    iconY += Units(1); // Adding first label height
                    iconY += (int)((Units(1) * (double)16 / 18)) * 2; // Adding 2nd and 3rd label heights
                    iconY += 4 * 3; // Adding 3 spacings
                    GUI.DrawTexture(new Rect(Units(2), iconY, Units(2), Units(2)), icon);
                }
                else
                {
                    GUI.DrawTexture(new Rect(Units(2), curY + Units(1), Units(2), Units(2)), icon);
                }
            }

            int btnWidth = Units(11);
            int halfWidth = (int)(rect.width / 2);

            var posY = curY + Units(1) - 8;

            int posX = VerticalLayout ? Units(2) : Units(5);

            var style = GUI.skin.label;

            style.fontSize -= VerticalLayout ? 0 : 4;
            GUI.Label(new Rect(posX, posY, rect.width - posX, Units(2)), $"{balance.Available.ToString(MoneyFormat)} {balance.Symbol} ({accountManager.GetTokenWorth(balance.Symbol, balance.Available)})");
            style.fontSize += VerticalLayout ? 0 : 4;

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
                            BeginWaitingModal("Preparing swap transaction...");
                            accountManager.SettleSwap(balance.PendingPlatform, accountManager.CurrentPlatform.ToString().ToLower(), balance.Symbol, balance.PendingHash, (settleHash, error) =>
                            {
                                EndWaitingModal();
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
                                    MessageBox(MessageKind.Error, $"There was some error claiming your {balance.Symbol}...\n{error}");
                                }
                            });
                        }
                    });
                };
            }
            else
                switch (balance.Symbol)
                {
                    case "MKNI":
                        // Temporary hide this button because of a crash in dapp.
                        /*if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                        {
                            secondaryAction = "Dapps";
                            secondaryEnabled = true;
                            secondaryCallback = () =>
                            {
                                PushState(GUIState.Dapps);
                            };
                        }*/
                        break;

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

                                                    SendTransaction($"Stake {selectedAmount} SOUL", script, null, "main", (hash) =>
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

                                                        SendTransaction($"Unstake {amount} SOUL", script, null, "main", (hash) =>
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

                                        SendTransaction($"Claim {balance.Claimable} KCAL", script, null, "main", (hash) =>
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
                        /*else
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
                                            if (amount <= 0.1m && balance.Available <= 0.1m)
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
                        }*/
                        break;

                    case "GAS":
                        {
                            if (accountManager.CurrentPlatform == PlatformKind.Neo)
                            {
                                secondaryAction = "Claim";
                                secondaryEnabled = balance.Claimable > 0;
                                secondaryCallback = () =>
                                {
                                    PromptBox($"Do you want to claim GAS?\nThere is {balance.Claimable} GAS available.", ModalYesNo, (result) =>
                                    {
                                        if (result == PromptResult.Success)
                                        {
                                            var claimGas = new Action<NeoKeys, List<UnspentEntry>, decimal, bool>((neoKeys, claimableTransactions, claimableGasAmount, fullyClaimable) =>
                                            {
                                                // We get fresh unspents for claim transaction.
                                                StartCoroutine(accountManager.neoApi.GetUnspent(neoKeys.Address, (unspent) =>
                                                {
                                                    // Claiming GAS finally.
                                                    StartCoroutine(accountManager.neoApi.ClaimGas(unspent, neoKeys, claimableTransactions, claimableGasAmount, (tx, error) =>
                                                    {
                                                        PopState();
                                                        MessageBox(MessageKind.Success, $"You claimed {claimableGasAmount} GAS{(fullyClaimable ? "" : ". Not all GAS was claimed, please try later")}!\nTransaction hash: {tx.Hash}");
                                                    }));
                                                }));
                                            });

                                            var keys = NeoKeys.FromWIF(accountManager.CurrentAccount.WIF);

                                            PushState(GUIState.Sending);

                                            // Getting currenly available claimable transactions and count them.
                                            StartCoroutine(accountManager.neoApi.GetClaimable(keys.Address, (claimableOriginal, amountOriginal) =>
                                            {
                                                // Getting unspents, needed for sending NEO to yourself.
                                                StartCoroutine(accountManager.neoApi.GetUnspent(keys.Address, (unspent) =>
                                                {
                                                    // Sending NEO to yourself - needed for claimable transactions update,
                                                    // to claim all generated GAS.
                                                    StartCoroutine(accountManager.neoApi.SendAsset((tx, error) =>
                                                    {
                                                        // Waiting for 2 seconds before checking if new claimable appeared in this list.
                                                        Thread.Sleep(2000);
                                                        StartCoroutine(accountManager.neoApi.GetClaimable(keys.Address, (claimable, amount) =>
                                                        {
                                                            // Checking if our new transaction appeared in claimables.
                                                            if (claimable.Count() > claimableOriginal.Count())
                                                            {
                                                                claimGas(keys, claimable, amount, true);
                                                            }
                                                            else
                                                            {
                                                                // We should wait more.
                                                                Log.Write("GAS claim: Claimable list not updated yet (1)...");
                                                                Thread.Sleep(4000);
                                                                StartCoroutine(accountManager.neoApi.GetClaimable(keys.Address, (claimable2, amount2) =>
                                                                {
                                                                    // Checking if our new transaction appeared in claimables.
                                                                    if (claimable2.Count() > claimableOriginal.Count())
                                                                    {
                                                                        claimGas(keys, claimable2, amount2, true);
                                                                    }
                                                                    else
                                                                    {
                                                                        // We should wait more.
                                                                        Log.Write("GAS claim: Claimable list not updated yet (2)...");
                                                                        Thread.Sleep(10000);
                                                                        StartCoroutine(accountManager.neoApi.GetClaimable(keys.Address, (claimable3, amount3) =>
                                                                        {
                                                                            // Checking if our new transaction appeared in claimables.
                                                                            if (claimable3.Count() > claimableOriginal.Count())
                                                                            {
                                                                                claimGas(keys, claimable3, amount3, true);
                                                                            }
                                                                            else
                                                                            {
                                                                                // Claiming what we can (not all).
                                                                                if (claimable3.Count() > 0)
                                                                                {
                                                                                    claimGas(keys, claimable3, amount3, false);
                                                                                }
                                                                                else
                                                                                {
                                                                                    PopState();
                                                                                    MessageBox(MessageKind.Success, $"Cannot claim GAS, please try later.");
                                                                                }
                                                                            }
                                                                        }));
                                                                    }
                                                                }));
                                                            }
                                                        }));
                                                    }, unspent, keys, keys.Address, "NEO", state.GetAvailableAmount("NEO"), null, 0, true));
                                                }));
                                            }));
                                        }
                                    });
                                };
                            }
                            break;
                        }

                    case "TTRS":
                        {
                            if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                            {
                                secondaryAction = "View";
                                secondaryEnabled = balance.Available > 0;
                                secondaryCallback = () =>
                                {
                                    AudioManager.Instance.PlaySFX("click");

                                    transferSymbol = balance.Symbol;

                                    // We should do this initialization here and not in PushState,
                                    // to allow "Back" button to work properly.
                                    nftScroll = Vector2.zero;
                                    nftTransferList.Clear();
                                    nftFilterName = "";
                                    nftFilterTypeIndex = 0;
                                    nftFilterType = "All";
                                    nftFilterRarity = 0;
                                    nftFilterMinted = 0;
                                    accountManager.RefreshNft(false, transferSymbol);

                                    PushState(GUIState.TtrsNftView);
                                    return;
                                };
                            }
                            break;
                        }
                }

            int btnY = VerticalLayout ? Units(4) + 8: Units(2);

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

                 accountManager.GetTokenBySymbol(transferSymbol, accountManager.CurrentPlatform, out transferToken);

                 if (string.IsNullOrEmpty(transferToken.flags))
                 {
                     MessageBox(MessageKind.Error, $"Operations with token {transferSymbol} are not supported yet in this version.");
                     return;
                 }

                 if (transferToken.flags.Contains(TokenFlags.Transferable.ToString()) && transferSymbol == "TTRS")
                 {
                     // We should do this initialization here and not in PushState,
                     // to allow "Back" button to work properly.
                     nftScroll = Vector2.zero;
                     nftTransferList.Clear();
                     nftFilterName = "";
                     nftFilterTypeIndex = 0;
                     nftFilterType = "All";
                     nftFilterRarity = 0;
                     nftFilterMinted = 0;
                     accountManager.RefreshNft(false, transferSymbol);

                     PushState(GUIState.TtrsNft);
                     return;
                 }

                 if (!transferToken.flags.Contains(TokenFlags.Transferable.ToString()))
                 {
                     MessageBox(MessageKind.Error, $"Transfers of {transferSymbol} tokens are not allowed.");
                     return;
                 }

                 ShowModal(transferName, "Enter destination address", ModalState.Input, 3, 64, ModalConfirmCancel, 1, (result, destAddress) =>
                 {
                     if (result == PromptResult.Failure)
                     {
                         return; // user canceled
                     }

                     var nethereumAddressUtil = new Nethereum.Util.AddressUtil();

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
                     if (nethereumAddressUtil.IsValidEthereumAddressHexFormat(destAddress))
                     {
                         if (accountManager.CurrentPlatform == PlatformKind.Ethereum)
                         {
                             ContinueEthTransfer(transferName, transferSymbol, destAddress);
                         }
                         else
                         if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                         {
                             ContinueSwap(PlatformKind.Ethereum, transferName, transferSymbol, destAddress);
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

                 modalHints = GenerateAccountHints(accountManager.CurrentPlatform.GetTransferTargets(transferToken));
             });
        }

        private void DoTtrsNftScreen()
        {
            var accountManager = AccountManager.Instance;

            var nfts = accountManager.CurrentNfts;
            if (accountManager.Refreshing)
            {
                DrawCenteredText((nfts != null) ? $"Fetching NFTs ({nfts.Count})..." : "Fetching NFTs...");
                return;
            }

            var startY = Units(VerticalLayout ? 11 : 7);
            var nftToolsY = startY;
            startY += (VerticalLayout) ? Units(6) : Units(4);
            var endY = DoBottomMenuForNft();

            if (nfts == null)
            {
                DrawCenteredText("Loading...");
                return;
            }

            // Sorting NFT list.
            accountManager.SortTtrsNfts();
            nfts = accountManager.CurrentNfts;

            // Filtering NFT list, if filters are applied.
            nftFilteredList.Clear();
            if (!String.IsNullOrEmpty(nftFilterName) || nftFilterType != "All" || nftFilterRarity != (int)ttrsNftRarity.All || nftFilterMinted != (int)nftMinted.All)
            {
                nfts.ForEach((x) => {
                    var item = TtrsStore.GetNft(x);

                    if ((String.IsNullOrEmpty(nftFilterName) || item.NameEnglish.ToUpper().Contains(nftFilterName.ToUpper())) &&
                        (nftFilterType == "All" || item.DisplayTypeEnglish == nftFilterType) &&
                        (nftFilterRarity == (int)ttrsNftRarity.All || (int)item.Rarity == nftFilterRarity) &&
                        (nftFilterMinted == (int)nftMinted.All ||
                         (nftFilterMinted == (int)nftMinted.Last_15_Mins && DateTime.Compare(item.Timestamp, DateTime.Now.AddMinutes(-15)) >= 0) ||
                         (nftFilterMinted == (int)nftMinted.Last_Hour && DateTime.Compare(item.Timestamp, DateTime.Now.AddHours(-1)) >= 0) ||
                         (nftFilterMinted == (int)nftMinted.Last_24_Hours && DateTime.Compare(item.Timestamp, DateTime.Now.AddDays(-1)) >= 0) ||
                         (nftFilterMinted == (int)nftMinted.Last_Week && DateTime.Compare(item.Timestamp, DateTime.Now.AddDays(-7)) >= 0) ||
                         (nftFilterMinted == (int)nftMinted.Last_Month && DateTime.Compare(item.Timestamp, DateTime.Now.AddMonths(-1)) >= 0)
                        ))
                    {
                        nftFilteredList.Add(x);
                    }
                });
                nfts = nftFilteredList;
            }

            // Number of displayed NFTs changed, switching to first page.
            if (nfts.Count != nftCount)
            {
                nftPageNumber = 0;
            }

            nftCount = nfts.Count;
            nftPageCount = nftCount / nftPageSize + 1;

            // Making NFT list for current page.
            var nftPage = new List<string>();
            for(int i = nftPageSize * nftPageNumber; i < Math.Min(nftPageSize * (nftPageNumber + 1), nfts.Count); i++)
            {
                nftPage.Add(nfts[i]);
            }
            var nftOnPageCount = DoScrollArea<string>(ref nftScroll, startY, endY, VerticalLayout ? Units(5) : Units(4), nftPage,
                DoTtrsNftEntry);

            if (nftOnPageCount == 0)
            {
                DrawCenteredText($"No TTRS NFTs found for this {accountManager.CurrentPlatform} account.");
            }

            DrawNftTools(nftToolsY);

            DrawPlatformTopMenu(() =>
            {
                accountManager.RefreshBalances(true);
                accountManager.RefreshNft(true, transferSymbol);
                accountManager.ResetTtrsNftsSorting();
            }, false);
        }

        // Used for both NFT list and transfer NFT list.
        private void DoTtrsNftEntry(string entryId, int index, int curY, Rect rect)
        {
            int panelHeight = Units(3);
            int panelWidth = (int)(windowRect.width - Units(2));
            int padding = 8;

            int availableHeight = (int)(windowRect.height - (curY + Units(6)));
            int heightPerItem = panelHeight + padding;
            int maxEntries = availableHeight / heightPerItem;

            var accountManager = AccountManager.Instance;

            int halfWidth = (int)(rect.width / 2);

            var item = TtrsStore.GetNft(entryId);

            if (!String.IsNullOrEmpty(item.NameEnglish))
            {
                var image = TtrsStore.GetImage(item.Img);

                if (!String.IsNullOrEmpty(image.Url))
                {
                    var textureDisplayedHeight = VerticalLayout ? Units(3) : Units(3) - 8;
                    GUI.DrawTexture(new Rect(Units(2), VerticalLayout ? curY + Units(1) : curY + 12, (float)textureDisplayedHeight * ((float)image.Texture.width / (float)image.Texture.height), textureDisplayedHeight), image.Texture);
                }
            }

            string rarity;
            switch(item.Rarity)
            {
                case 1:
                    rarity = VerticalLayout ? "/Con" : " / Consumer";
                    break;
                case 2:
                    rarity = VerticalLayout ? "/Ind" : " / Industrial";
                    break;
                case 3:
                    rarity = VerticalLayout ? "/Pro" : " / Professional";
                    break;
                case 4:
                    rarity = VerticalLayout ? "/Col" : " / Collector";
                    break;
                default:
                    rarity = "";
                    break;
            }

            var nftName = item.NameEnglish;
            if (String.IsNullOrEmpty(nftName))
                nftName = "Loading...";
            if (VerticalLayout && nftName.Length > 18)
                nftName = nftName.Substring(0, 15) + "...";

            var nftType = item.DisplayTypeEnglish;
            if (VerticalLayout)
            {
                switch (nftType)
                {
                    case "Vehicle":
                        nftType = "Veh";
                        break;
                    case "Part":
                        nftType = "Prt";
                        break;
                    case "License":
                        nftType = "Lic";
                        break;
                    default:
                        break;
                }
            }

            var nftDescription = item.Mint == 0 ? "" : (VerticalLayout ? "#" : "Mint #") + item.Mint + " " + (VerticalLayout ? item.Timestamp.ToString("dd.MM.yy") : item.Timestamp.ToString("dd.MM.yyyy HH:mm:ss")) + (VerticalLayout ? " " : " / ") + nftType + rarity;

            GUI.Label(new Rect(VerticalLayout ? Units(7) : Units(6) + 8, VerticalLayout ? curY + 4 : curY, rect.width - Units(6), Units(2) + 4), nftName);

            if (!String.IsNullOrEmpty(nftDescription))
            {
                var style = GUI.skin.label;
                style.fontSize -= VerticalLayout ? 2 : 4;
                GUI.Label(new Rect(VerticalLayout ? Units(7) : Units(6) + 8, VerticalLayout ? curY + Units(2) + 4 : curY + Units(1) + 8, rect.width - Units(6), Units(2)), nftDescription);
                style.fontSize += VerticalLayout ? 2 : 4;
            }

            Rect btnRectToggle;
            Rect btnRect;

            if (VerticalLayout)
            {
                curY += Units(2);
                btnRectToggle = new Rect(rect.x + rect.width - Units(8), curY - 4, Units(1), Units(1));
                btnRect = new Rect(rect.x + rect.width - Units(6), curY, Units(4), Units(1));
            }
            else
            {
                btnRectToggle = new Rect(rect.x + rect.width - Units(8), curY + Units(1) + 4, Units(1), Units(1));
                btnRect = new Rect(rect.x + rect.width - Units(6), curY + Units(1) + 8, Units(4), Units(1));
            }

            if (DrawNftToolsAreActive())
            {
                GUI.enabled = false;
            }
            if (guiState != GUIState.TtrsNftView)
            {
                var nftIsSelected = nftTransferList.Exists(x => x == entryId);
                if (GUI.Toggle(btnRectToggle, nftIsSelected, ""))
                {
                    if (!nftIsSelected)
                    {
                        nftTransferList.Add(entryId);
                    }
                }
                else
                {
                    if (nftIsSelected)
                    {
                        nftTransferList.Remove(nftTransferList.Single(x => x == entryId));
                    }
                }
            }
            GUI.enabled = true;

            DoButton(!DrawNftToolsAreActive(), btnRect, "View", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                Application.OpenURL("https://www.22series.com/part_info?id=" + item.Id);
            });

            curY += panelHeight + padding;
        }

        private void DoTtrsNftTransferListScreen()
        {
            var accountManager = AccountManager.Instance;

            var startY = DrawPlatformTopMenu(() =>
            {
            }, false);
            var endY = DoBottomMenuForNftTransferList();

            // We have to remake whole list to have correct order of selected items.
            var nftTransferListCopy = new List<string>();
            accountManager.CurrentNfts.ForEach((x) => { if (nftTransferList.Exists(y => y == x)) { nftTransferListCopy.Add(x); } });
            nftTransferList = nftTransferListCopy;

            // We can modify nftTransferList while enumerating,
            // so we should use a copy of it.
            nftTransferListCopy = new List<string>();
            nftTransferList.ForEach(x => nftTransferListCopy.Add(x));

            var nftTransferCount = DoScrollArea<string>(ref nftTransferListScroll, startY, endY, VerticalLayout ? Units(5) : Units(4), nftTransferListCopy,
                DoTtrsNftEntry);

            if (nftTransferCount == 0)
            {
                DrawCenteredText($"No NFTs selected for transfer.");
            }
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
                btnRect = new Rect(rect.x + rect.width - Units(6), curY - 8, Units(4), Units(1));
            }
            else
            {
                GUI.Label(new Rect(Units(26), curY + 4, Units(20), Units(2)), date);
                btnRect = new Rect(rect.x + rect.width - Units(6), curY + Units(1), Units(4), Units(1));
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
                var qrRect = new Rect((windowRect.width - qrResolution) / 2, VerticalLayout ? curY + Units(2) : curY, qrResolution, qrResolution);

                DrawDropshadow(qrRect);
                GUI.DrawTexture(qrRect, qrTex);
                curY += qrResolution;
                curY += Units(1);
            }

            curY = endY - Units(3);

            int btnOffset = Units(4);

            if (VerticalLayout)
            {
                btnOffset += Units(6);
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
                            ShowModal("Private key export", $"Copy private key in Wallet Import Format (WIF) or in HEX format to the clipboard" +
                                "\n\nWIF can be used to import wallet in all Phantasma wallets, including Poltergeist, Phantom and Ecto." +
                                "\nWIF format example (52 symbols):" +
                                "\nKz9xQgW1U49x8d6yijwLaBgN9x5zEdZaqkjLaS88ZnagcmBjckNE" +
                                "\n\nHEX can be used to import wallet in MEW Ethereum wallet and Neon Neo wallet." +
                                "\nHEX format example (64 symbols):" +
                                "\n5794a280d6d69c676855d6ffb63b40b20fde3c79d557cd058c95cd608a933fc3",
                                ModalState.Message, 0, 0, ModalHexWif, 0, (result, input) =>
                            {
                                AudioManager.Instance.PlaySFX("click");

                                if (result == PromptResult.Success)
                                {
                                    var keys = EthereumKey.FromWIF(accountManager.CurrentAccount.WIF);
                                    GUIUtility.systemCopyBuffer = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(keys.PrivateKey);
                                    MessageBox(MessageKind.Default, "Private key (HEX format) copied to the clipboard.");
                                }
                                else
                                {
                                    GUIUtility.systemCopyBuffer = accountManager.CurrentAccount.WIF;
                                    MessageBox(MessageKind.Default, "Private key (WIF format) copied to the clipboard.");
                                }
                            });
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

                                                    SendTransaction($"Register address name\nName:{name}\nAddress:{accountManager.CurrentState.address}?", script, null, "main", (hash) =>
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

                                            SendTransaction("Migrate account", script, null, DomainSettings.RootChainName, (hash) =>
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

        private string[] accountMenu = new string[] { "Export Private Key", "Setup Name", "Migrate", "Delete Account" };
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

        private int DoBottomMenuForNft()
        {
            int posY;

            var border = Units(1);

            int panelHeight = VerticalLayout ? Border * 2 + (Units(2) + 4) * 3 : (border + Units(3));
            posY = (int)((windowRect.y + windowRect.height) - (panelHeight + border));

            var rect = new Rect(border, posY, windowRect.width - border * 2, panelHeight);

            int halfWidth = (int)(windowRect.width / 2);
            int btnWidth = VerticalLayout ? Units(7) : Units(11);

            // Close
            DoButton(true, new Rect(VerticalLayout ? rect.x + border * 2 : (halfWidth - btnWidth) / 2,
                                    VerticalLayout ? (int)rect.y + border + (Units(2) + 4) * 2 : (int)rect.y + border,
                                    VerticalLayout ? rect.width - border * 4 : btnWidth, Units(2)), "Close", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                PushState(GUIState.Balances);

                // Saving sorting.
                AccountManager.Instance.Settings.SaveOnExit();
            });

            int pageLabelWidth = Units(4);
            int pageButtonWidth = Units(2);
            int pageButtonSpacing = 12;

            // <<
            DoButton(nftPageNumber > 0, new Rect(halfWidth - pageLabelWidth / 2 - (pageButtonWidth + pageButtonSpacing) * 2,
                                                 VerticalLayout ? (int)rect.y + border : (int)rect.y + border,
                                                 pageButtonWidth, Units(2)), "<<", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                nftPageNumber = 0;
            });

            // <
            DoButton(nftPageNumber > 0, new Rect(halfWidth - pageLabelWidth / 2 - (pageButtonWidth + pageButtonSpacing),
                                                 VerticalLayout ? (int)rect.y + border : (int)rect.y + border,
                                                 pageButtonWidth, Units(2)), "<", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                nftPageNumber--;
            });

            // Current page number
            var style = GUI.skin.GetStyle("Label");
            var prevAlignment = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;
            if (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.black;
            GUI.Label(new Rect(halfWidth - pageLabelWidth / 2 - 6,
                               (int)rect.y + 12,
                               pageLabelWidth, Units(2)), (nftPageNumber + 1).ToString(), style);
            if (AccountManager.Instance.Settings.uiThemeName == UiThemes.Classic.ToString())
                GUI.contentColor = Color.white;
            style.alignment = prevAlignment;

            // >
            DoButton(nftPageNumber < nftPageCount - 1, new Rect(halfWidth + pageLabelWidth / 2 + pageButtonSpacing,
                                                                VerticalLayout ? (int)rect.y + border : (int)rect.y + border,
                                                                pageButtonWidth, Units(2)), ">", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                nftPageNumber++;
            });

            // >>
            DoButton(nftPageNumber < nftPageCount - 1, new Rect(halfWidth + pageLabelWidth / 2 + pageButtonWidth + pageButtonSpacing * 2,
                                                                VerticalLayout ? (int)rect.y + border : (int)rect.y + border,
                                                                pageButtonWidth, Units(2)), ">>", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                nftPageNumber = nftPageCount - 1;
            });

            if (guiState != GUIState.TtrsNftView)
            {
                // To transfer list
                DoButton(nftTransferList.Count > 0, new Rect(VerticalLayout ? rect.x + border * 2 : halfWidth + (halfWidth - btnWidth) / 2,
                                        VerticalLayout ? (int)rect.y + border + (Units(2) + 4) : (int)rect.y + border,
                                        VerticalLayout ? rect.width - border * 4 : btnWidth, Units(2)), "To transfer list", () =>
                {
                    var nftTransferLimit = 100;
                    if (nftTransferList.Count > nftTransferLimit)
                    {
                        PromptBox($"Currently sending is limited to {nftTransferLimit} NFTs for one transfer, reduce selection to first {nftTransferLimit}? ", ModalConfirmCancel, (result) =>
                        {
                            if (result == PromptResult.Success)
                            {
                                nftTransferList.RemoveRange(nftTransferLimit, nftTransferList.Count - nftTransferLimit);
                                PushState(GUIState.TtrsNftTransferList);
                            }
                        });
                    }
                    else
                    {
                        PushState(GUIState.TtrsNftTransferList);
                    }
                });
            }
            else
            {
                // Online inventory
                DoButton(true, new Rect(VerticalLayout ? rect.x + border * 2 : halfWidth + (halfWidth - btnWidth) / 2,
                                        VerticalLayout ? (int)rect.y + border + (Units(2) + 4) : (int)rect.y + border,
                                        VerticalLayout ? rect.width - border * 4 : btnWidth, Units(2)), "Online inventory", () =>
                                        {
                                            AudioManager.Instance.PlaySFX("click");
                                            Application.OpenURL("https://www.22series.com/inventory?#" + AccountManager.Instance.GetAddress(AccountManager.Instance.CurrentIndex, AccountManager.Instance.CurrentPlatform));
                                        });
            }

            return posY;
        }

        private int DoBottomMenuForNftTransferList()
        {
            int posY;

            var border = Units(1);

            int panelHeight = VerticalLayout ? Border * 2 + (Units(2) + 4) * 2 : (border + Units(3));
            posY = (int)((windowRect.y + windowRect.height) - (panelHeight + border));

            var rect = new Rect(border, posY, windowRect.width - border * 2, panelHeight);

            int halfWidth = (int)(windowRect.width / 2);
            int btnWidth = VerticalLayout ? Units(7) : Units(11);

            // Back
            DoButton(true, new Rect(VerticalLayout ? rect.x + border * 2 : (halfWidth - btnWidth) / 2, VerticalLayout ? (int)rect.y + border + (Units(2) + 4) : (int)rect.y + border, VerticalLayout ? rect.width - border * 4 : btnWidth, Units(2)), "Back", () =>
            {
                AudioManager.Instance.PlaySFX("click");
                PushState(GUIState.TtrsNft);
            });

            // Send
            DoButton(nftTransferList.Count > 0, new Rect(VerticalLayout ? rect.x + border * 2 : halfWidth + (halfWidth - btnWidth) / 2, VerticalLayout ? (int)rect.y + border : (int)rect.y + border, VerticalLayout ? rect.width - border * 4 : btnWidth, Units(2)), "Send", () =>
            {
                AudioManager.Instance.PlaySFX("click");

                var accountManager = AccountManager.Instance;
                var state = accountManager.CurrentState;
                var transferName = $"{transferSymbol} transfer";
                Phantasma.SDK.Token transferToken;

                accountManager.GetTokenBySymbol(transferSymbol, accountManager.CurrentPlatform, out transferToken);

                if (string.IsNullOrEmpty(transferToken.flags))
                {
                    MessageBox(MessageKind.Error, $"Operations with token {transferSymbol} are not supported yet in this version.");
                    return;
                }

                if (!transferToken.flags.Contains(TokenFlags.Transferable.ToString()))
                {
                    MessageBox(MessageKind.Error, $"Transfers of {transferSymbol} tokens are not allowed.");
                    return;
                }

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
                            ContinuePhantasmaNftTransfer(transferName, transferSymbol, destAddress);
                        }
                        else
                        {
                            MessageBox(MessageKind.Error, $"Direct transfers from {accountManager.CurrentPlatform} to this type of address not supported.");
                        }
                    }
                    else
                    if (Phantasma.Neo.Utils.NeoUtils.IsValidAddress(destAddress))
                    {
                        MessageBox(MessageKind.Error, $"Direct transfers from {accountManager.CurrentPlatform} to this type of address not supported.");
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
                                ContinuePhantasmaNftTransfer(transferName, transferSymbol, lookupAddress);
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

                modalHints = GenerateAccountHints(accountManager.CurrentPlatform.GetTransferTargets(transferToken));
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

        public void SendTransaction(string description, byte[] script, byte[] payload, string chain, Action<Hash> callback)
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
                var feeDecimals = accountManager.GetTokenDecimals("KCAL", accountManager.CurrentPlatform);
                description += $"\nEstimated fee: {UnitConversion.ToDecimal(estimatedFee, feeDecimals)} KCAL";
            }
            else if (accountManager.CurrentPlatform == PlatformKind.Neo)
            {
                description += $"\nFee: {accountManager.Settings.neoGasFee} GAS";
            }
            else if (accountManager.CurrentPlatform == PlatformKind.Ethereum)
            {
                BigInteger usedGas;

                var transfer = Serialization.Unserialize<TransferRequest>(script);
                if (transfer.platform == PlatformKind.Ethereum)
                {
                    if (transfer.symbol == "ETH")
                    {
                        // Eth transfer.
                        usedGas = accountManager.Settings.ethereumTransferGasLimit;
                    }
                    else
                    {
                        // Token transfer.
                        usedGas = accountManager.Settings.ethereumTokenTransferGasLimit;
                    }

                    var estimatedFee = usedGas * accountManager.Settings.ethereumGasPriceGwei;
                    description += $"\nEstimated fee: {UnitConversion.ToDecimal(estimatedFee, 9)} ETH"; // 9 because we convert from Gwei, not Wei
                }
            }

            RequestPassword(description, accountManager.CurrentPlatform, (auth) =>
            {
                if (auth == PromptResult.Success)
                {
                    Animate(AnimationDirection.Right, true, () =>
                    {
                        Animate(AnimationDirection.Left, false, () =>
                        {
                            PromptBox($"Preparing transaction...\n{description}", ModalSendCancel, (result) =>
                            {
                                if (result == PromptResult.Success)
                                {
                                    PushState(GUIState.Sending);

                                    accountManager.SignAndSendTransaction(chain, script, payload, null, (hash, error) =>
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
                                                callback(Hash.Null);
                                            });
                                        }
                                    });
                                }
                                else
                                {
                                    callback(Hash.Null);
                                };
                            });
                        });
                    });
                }
                else
                if (auth == PromptResult.Failure)
                {
                    MessageBox(MessageKind.Error, $"Authorization failed.", () =>
                    {
                        callback(Hash.Null);
                    });
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
            RequireAmount(transferName, destAddress, symbol, 0.001m, balance, (amount) =>
            {
                RequestKCAL(symbol, (feeResult) =>
                {
                    if (feeResult == PromptResult.Success)
                    {
                        byte[] script;

                        try
                        {
                            var decimals = accountManager.GetTokenDecimals(symbol, accountManager.CurrentPlatform);

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

                        SendTransaction($"Transfer {amount.ToString(MoneyFormatLong)} {symbol}\nDestination: {destination}", script, null, "main", (hash) =>
                        {
                            if (hash != Hash.Null)
                            {
                                ShowModal("Success",
                                    $"You transfered {amount.ToString(MoneyFormatLong)} {symbol}!\nTransaction hash:\n" + hash,
                                    ModalState.Message, 0, 0, ModalOkView, 0, (viewTxChoice, input) =>
                                    {
                                        AudioManager.Instance.PlaySFX("click");
                                    
                                        if (viewTxChoice == PromptResult.Failure)
                                        {
                                            Application.OpenURL(accountManager.GetPhantasmaTransactionURL(hash.ToString()));
                                        }
                                    });
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

        private void ContinuePhantasmaNftTransfer(string transferName, string symbol, string destAddress)
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
            var amount = nftTransferList.Count;
            RequestKCAL(symbol, (feeResult) =>
            {
                if (feeResult == PromptResult.Success)
                {
                    byte[] script;
                    string description;

                    try
                    {
                        description = $"Transfer {symbol} NFTs\n";

                        var decimals = accountManager.GetTokenDecimals(symbol, accountManager.CurrentPlatform);

                        var gasPrice = accountManager.Settings.feePrice;

                        var sb = new ScriptBuilder();
                        sb.AllowGas(source, Address.Null, gasPrice, AccountManager.MinGasLimit * nftTransferList.Count);

                        foreach (var nft in nftTransferList)
                        {
                            sb.TransferNFT(symbol, source, destination, BigInteger.Parse(nft));

                            string nftDescription = "";
                            if(symbol == "TTRS")
                            {
                                var item = TtrsStore.GetNft(nft);
                                
                                nftDescription = " " + ((item.NameEnglish.Length > 25) ? item.NameEnglish.Substring(0, 22) + "..." : item.NameEnglish);

                                nftDescription += " Minted " + item.Timestamp.ToString("dd.MM.yy") + " #" + item.Mint;
                            }

                            description += $"#{nft.Substring(0, 5) + "..." + nft.Substring(nft.Length - 5)}{nftDescription}\n";
                        }

                        sb.SpendGas(source);
                        script = sb.EndScript();

                        description += $"to {destination}.";
                    }
                    catch (Exception e)
                    {
                        MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message);
                        return;
                    }

                    SendTransaction(description, script, null, "main", (hash) =>
                    {
                        if (hash != Hash.Null)
                        {
                            ShowModal("Success",
                                $"You transfered {amount.ToString(MoneyFormatLong)} {symbol}!\nTransaction hash:\n" + hash,
                                ModalState.Message, 0, 0, ModalOkView, 0, (viewTxChoice, input) =>
                                {
                                    AudioManager.Instance.PlaySFX("click");

                                    if (viewTxChoice == PromptResult.Failure)
                                    {
                                        Application.OpenURL(accountManager.GetPhantasmaTransactionURL(hash.ToString()));
                                    }
                                });

                            // Removing sent NFTs from current NFT list.
                            var nfts = accountManager.CurrentNfts;
                            foreach (var nft in nftTransferList)
                            {
                                nfts.Remove(nfts.Find(x => x == nft));
                            }

                            // Returning to NFT's first screen.
                            nftScroll = Vector2.zero;
                            nftTransferList.Clear();
                            PushState(GUIState.TtrsNft);
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

        private bool ValidDecimals(decimal amount, string symbol)
        {
            var decimals = AccountManager.Instance.GetTokenDecimals(symbol, AccountManager.Instance.CurrentPlatform);

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
            var caption = $"Enter {symbol} amount:\nMax: {max.ToString(MoneyFormatLong)} {symbol}";
            if (symbol == "GAS" && accountManager.CurrentPlatform == PlatformKind.Phantasma && destination == null)
            {
                caption += "\nWarning: Swapping back consumes GAS (around 0.1) so if your GAS balance falls below that, swap back to NEO will fail.";
            }

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

            modalHints = new Dictionary<string, string>() { { $"Max ({max.ToString(MoneyFormatShort)} {symbol})", max.ToString() } };
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

            var min = accountManager.Settings.neoGasFee;
            RequestFee(symbol, "GAS", min, (gasResult) =>
            {
                if (gasResult != PromptResult.Success)
                {
                    MessageBox(MessageKind.Error, $"Without at least {min} GAS it is not possible to perform this transfer!");
                    return;
                }

                var balance = state.GetAvailableAmount(symbol);
                if (symbol == "GAS")
                    balance -= min;

                RequireAmount(transferName, destAddress, symbol, 0.001m, balance, (amount) =>
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

                    SendTransaction($"Transfer {amount.ToString(MoneyFormatLong)} {symbol}\nDestination: {destAddress}", script, null, transfer.platform.ToString(), (hash) =>
                    {
                        if (hash != Hash.Null)
                        {
                            ShowModal("Success",
                                $"You transfered {amount.ToString(MoneyFormatLong)} {symbol}!\nTransaction hash:\n" + hash,
                                ModalState.Message, 0, 0, ModalOkView, 0, (viewTxChoice, input) =>
                                {
                                    AudioManager.Instance.PlaySFX("click");

                                    if (viewTxChoice == PromptResult.Failure)
                                    {
                                        Application.OpenURL(accountManager.GetNeoscanTransactionURL(hash.ToString()));
                                    }
                                });
                        }
                    });
                });
            });
        }

        private void ContinueEthTransfer(string transferName, string symbol, string destAddress)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Ethereum)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Ethereum);
                return;
            }

            var sourceAddress = accountManager.GetAddress(accountManager.CurrentIndex, accountManager.CurrentPlatform);

            if (sourceAddress == destAddress)
            {
                MessageBox(MessageKind.Error, $"Source and destination address must be different!");
                return;
            }

            var ethBalance = accountManager.CurrentState.GetAvailableAmount("ETH");
            if (ethBalance <= 0)
            {
                MessageBox(MessageKind.Error, $"You will need at least a drop of ETH in this wallet to make a transaction.");
                return;
            }

            var balance = state.GetAvailableAmount(symbol);
            EthGasStationRequest((safeLow, safeLowWait, standard, standardWait, fast, fastWeight, fastest, fastestWeight) =>
            {
                EditBigIntegerFee("Set transaction gas price in GWEI", accountManager.Settings.ethereumGasPriceGwei, safeLow, safeLowWait, standard, standardWait, fast, fastWeight, fastest, fastestWeight, (result, fee) =>
                {
                    if (result == PromptResult.Success)
                    {
                        accountManager.Settings.ethereumGasPriceGwei = fee;
                        accountManager.Settings.SaveOnExit();

                        BigInteger usedGas;
                        if (symbol == "ETH")
                        {
                            // Eth transfer.
                            usedGas = accountManager.Settings.ethereumTransferGasLimit;
                        }
                        else
                        {
                            // Simple token transfer.
                            usedGas = accountManager.Settings.ethereumTokenTransferGasLimit;
                        }

                        var decimalFee = UnitConversion.ToDecimal(usedGas * accountManager.Settings.ethereumGasPriceGwei, 9); // 9 because we convert from Gwei, not Wei

                        RequestFee(symbol, "ETH", decimalFee, (feeResult) =>
                        {
                            if (feeResult != PromptResult.Success)
                            {
                                MessageBox(MessageKind.Error, $"Without at least {decimalFee} ETH it is not possible to perform this transfer!");
                                return;
                            }

                            balance -= decimalFee;

                            RequireAmount(transferName, destAddress, symbol, 0.001m, balance, (amount) =>
                            {
                                var transfer = new TransferRequest()
                                {
                                    platform = PlatformKind.Ethereum,
                                    amount = amount,
                                    symbol = symbol,
                                    key = accountManager.CurrentAccount.WIF,
                                    destination = destAddress
                                };

                                byte[] script = Serialization.Serialize(transfer);

                                SendTransaction($"Transfer {amount.ToString(MoneyFormatLong)} {symbol}\nDestination: {destAddress}", script, null, transfer.platform.ToString(), (hash) =>
                                {
                                    if (hash != Hash.Null)
                                    {
                                        ShowModal("Success",
                                            $"You sent transaction transferring {amount.ToString(MoneyFormatLong)} {symbol}!\nPlease use Ethereum explorer to ensure transaction is confirmed successfully and funds are transferred (button 'View' below).\nTransaction hash:\n" + hash,
                                            ModalState.Message, 0, 0, ModalOkView, 0, (viewTxChoice, input) =>
                                            {
                                                AudioManager.Instance.PlaySFX("click");

                                                if (viewTxChoice == PromptResult.Failure)
                                                {
                                                    Application.OpenURL(accountManager.GetEtherscanTransactionURL(hash.ToString()));
                                                }
                                            });
                                    }
                                });
                            });
                        });
                    }
                });
            });
        }

        private void ContinueSwap(PlatformKind destPlatform, string transferName, string swappedSymbol, string destination)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            var sourceAddress = accountManager.GetAddress(accountManager.CurrentIndex, accountManager.CurrentPlatform);

            if (sourceAddress == destination)
            {
                MessageBox(MessageKind.Error, $"Source and destination address must be different!");
                return;
            }

            // We set GAS as main fee symbol for both NEO -> PHA and PHA -> NEO swaps.
            // We set ETH as main fee symbol for both ETH -> PHA and PHA -> ETH swaps.
            // When we do swaps from PHA, KCAL also used for tx sending.
            // When we do swaps to PHA, transfered token is partially cosmic-swapped to KCAL by bp, it's automatic.
            var feeSymbol0 = "GAS";
            if (accountManager.CurrentPlatform == PlatformKind.Ethereum || destPlatform == PlatformKind.Ethereum)
                feeSymbol0 = "ETH";

            var proceedWithSwap = new Action<string, string, decimal>((symbol, feeSymbol, min) =>
            {
                // Set proper min value depending on platform.
                if (accountManager.CurrentPlatform == PlatformKind.Neo)
                {
                    min = Math.Max(0.001m, accountManager.Settings.neoGasFee);
                }
                else if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                {
                    // Since these fees are calculated and taken by BP,
                    // and we don't have a way of finding them out (at least for now)
                    // we have to set them with a margin.
                    if (destPlatform == PlatformKind.Neo)
                        min = 0.1m; // For Neo use 0.1 GAS constant
                    else if (destPlatform == PlatformKind.Ethereum)
                        min *= 2; // Very rough calculation for now - we ask user to have twice the amount of ETH that's needed for transaction.
                }

                RequestFee(symbol, feeSymbol, min, (gasResult) =>
                {
                    if (gasResult != PromptResult.Success)
                    {
                        MessageBox(MessageKind.Error, $"Without at least {min} {feeSymbol} it is not possible to perform this swap!");
                        return;
                    }

                    var balance = state.GetAvailableAmount(symbol);
                    if (symbol == "GAS" || symbol == "ETH")
                        balance -= min;

                    RequireAmount(transferName, null, symbol, 0.001m, balance, (amount) =>
                    {
                        if (accountManager.CurrentPlatform == PlatformKind.Phantasma)
                        {
                            Address destAddress;

                            switch (destPlatform)
                            {
                                case PlatformKind.Neo:
                                    destAddress = AccountManager.EncodeNeoAddress(destination);
                                    break;
                                case PlatformKind.Ethereum:
                                    destAddress = AccountManager.EncodeEthereumAddress(destination);
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
                                        var decimals = accountManager.GetTokenDecimals(symbol, accountManager.CurrentPlatform);

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

                                    SendTransaction($"Transfer {amount.ToString(MoneyFormatLong)} {symbol}\nDestination: {destination}", script, null, "main", (hash) =>
                                    {
                                        if (hash != Hash.Null)
                                        {
                                            ShowModal("Success",
                                                $"You transfered {amount.ToString(MoneyFormatLong)} {symbol}!\nTransaction hash:\n" + hash,
                                                ModalState.Message, 0, 0, ModalOkView, 0, (viewTxChoice, input) =>
                                                {
                                                    AudioManager.Instance.PlaySFX("click");

                                                    if (viewTxChoice == PromptResult.Failure)
                                                    {
                                                        Application.OpenURL(accountManager.GetPhantasmaTransactionURL(hash.ToString()));
                                                    }

                                                    accountManager.RefreshBalances(false);
                                                });
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
                                    Log.Write("Found interop address: " + interopAddress);

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

                                    SendTransaction($"Transfer {amount.ToString(MoneyFormatLong)} {symbol}\nDestination: {destination}", script, null, transfer.platform.ToString(), (hash) =>
                                    {
                                        if (hash != Hash.Null)
                                        {
                                            string successMessage;
                                            if(accountManager.CurrentPlatform == PlatformKind.Ethereum)
                                                successMessage = $"You sent transaction transferring {amount.ToString(MoneyFormatLong)} {symbol}!\nPlease use Ethereum explorer to ensure transaction is confirmed successfully and funds are transferred (button 'View' below).\nTransaction hash:\n" + hash;
                                            else
                                                successMessage = $"You transfered {amount.ToString(MoneyFormatLong)} {symbol}!\nTransaction hash:\n" + hash;

                                            ShowModal("Success",
                                                successMessage,
                                                ModalState.Message, 0, 0, ModalOkView, 0, (viewTxChoice, input) =>
                                                {
                                                    AudioManager.Instance.PlaySFX("click");

                                                    if (viewTxChoice == PromptResult.Failure)
                                                    {
                                                        if(accountManager.CurrentPlatform == PlatformKind.Neo)
                                                            Application.OpenURL(accountManager.GetNeoscanTransactionURL(hash.ToString()));
                                                        else if (accountManager.CurrentPlatform == PlatformKind.Ethereum)
                                                            Application.OpenURL(accountManager.GetEtherscanTransactionURL(hash.ToString()));
                                                    }

                                                    accountManager.RefreshBalances(false);
                                                });
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
            });

            if (feeSymbol0 == "ETH")
            {
                EthGasStationRequest((safeLow, safeLowWait, standard, standardWait, fast, fastWeight, fastest, fastestWeight) =>
                {
                    var decimalFee = UnitConversion.ToDecimal((swappedSymbol == "ETH" ? accountManager.Settings.ethereumTransferGasLimit : accountManager.Settings.ethereumTokenTransferGasLimit) * fast, 9); // 9 because we convert from Gwei, not Wei

                    proceedWithSwap(swappedSymbol, feeSymbol0, decimalFee);
                });
            }
            else
            {
                // For Neo all fees are taken from constants or settings and set inside proceedWithSwap(), that's why we pass 0 here.
                proceedWithSwap(swappedSymbol, feeSymbol0, 0);
            }
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

            var swapDecimals = accountManager.GetTokenDecimals(swapSymbol, accountManager.CurrentPlatform);
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

                                 var decimals = accountManager.GetTokenDecimals(feeSymbol, accountManager.CurrentPlatform);

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

                             SendTransaction($"Swap {swapSymbol} for {feeSymbol}", script, null, "main", (hash) =>
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

        private void EditBigIntegerFee(string message, BigInteger fee, BigInteger safeLow, string safeLowWait, BigInteger standard, string standardWait, BigInteger fast, string fastWait, BigInteger fastest, string fastestWait, Action<PromptResult, BigInteger> callback)
        {
            var accountManager = AccountManager.Instance;
            var state = accountManager.CurrentState;

            ShowModal("Fees", $"{message}:", ModalState.Input, 1, 64, ModalConfirmCancel, 1,
                (result, input) =>
                {
                    if (result == PromptResult.Success)
                    {
                        if (!String.IsNullOrEmpty(input) && input.All(char.IsDigit))
                        {
                            fee = new BigInteger(input, 10);
                            if (fee < safeLow)
                            {
                                PromptBox($"You set fee lower than safe low ({fee} < {safeLow}), transfer might take too long or fail.\nAre you sure you want to continue?", ModalYesNo, (wantToContinue) =>
                                {
                                    if (wantToContinue == PromptResult.Success)
                                    {
                                        callback(PromptResult.Success, fee);
                                    }
                                    else
                                    {
                                        return;
                                    }
                                });
                            }
                            else
                            {
                                callback(PromptResult.Success, fee);
                            }
                        }
                        else
                        {
                            MessageBox(MessageKind.Error, "Invalid fee!");
                            return;
                        }
                    }
                    else
                    {
                        callback(result, 0);
                    }
                });

            modalInput = fee.ToString();
            modalHintsLabel = "Gas prices";
            modalHints = new Dictionary<string, string>() { { $"Safe low: {safeLow} ({safeLowWait} min)", safeLow.ToString() },
                { $"Standard: {standard} ({standardWait} min)", standard.ToString() },
                { $"Fast: {fast} ({fastWait} min)", fast.ToString() },
                { $"Fastest: {fastest} ({fastestWait} min)", fastest.ToString() } };
        }

        private void EthGasStationRequest(Action<BigInteger, string, BigInteger, string, BigInteger, string, BigInteger, string> callback)
        {
            var url = "https://ethgasstation.info/api/ethgasAPI.json?api-key=25d4c0f579cd9d98ac8a124269a0f752e598882a2e7f6fcbdb0c8aa6bbb9";
            StartCoroutine(WebClient.RESTRequest(url, (error, msg) =>
                {
                    Log.Write("EthGasStationRequest error: " + error);
                    callback(0, "", 0, "", 0, "", 0, "");
                },
                (response) =>
                {
                    callback(response.GetInt32("safeLow", 0) / 10,
                        response.GetString("safeLowWait"),
                        response.GetInt32("average", 0) / 10,
                        response.GetString("avgWait"),
                        response.GetInt32("fast", 0) / 10,
                        response.GetString("fastWait"),
                        response.GetInt32("fastest", 0) / 10,
                        response.GetString("fastestWait"));
                }));
        }
        #endregion

        private Dictionary<string, string> GenerateAccountHints(PlatformKind targets)
        {
            var accountManager = AccountManager.Instance;
            var hints = new Dictionary<string, string>();

            hints["Scan QR"] = $"|{GUIState.ScanQR}";

            // Adding this account addresses at the beggining of item list.
            var platformsForCurrentAccount = accountManager.CurrentAccount.platforms.Split();

            foreach (var platform in platformsForCurrentAccount)
            {
                if (platform == accountManager.CurrentPlatform)
                {
                    continue;
                }

                if (targets.HasFlag(platform))
                {
                    var addr = accountManager.GetAddress(accountManager.CurrentIndex, platform);
                    if (!string.IsNullOrEmpty(addr))
                    {
                        var shortenedPlatform = platform.ToString();
                        if (platform.ToString() == "Phantasma")
                        {
                            shortenedPlatform = "Pha";
                        }
                        var key = $"{accountManager.CurrentAccount.name} [{shortenedPlatform}]";
                        hints[key] = addr;
                    }
                }
            }

            for (int index=0; index< accountManager.Accounts.Length; index++)
            {
                var account = accountManager.Accounts[index];
                var platforms = account.platforms.Split();

                foreach (var platform in platforms)
                {
                    if (account.name == accountManager.CurrentAccount.name)
                    {
                        continue;
                    }

                    if (targets.HasFlag(platform))
                    {
                        if(accountManager.CurrentPlatform == PlatformKind.Ethereum && platform == PlatformKind.Phantasma)
                        {
                            // We supports Eth swaps (Eth -> Pha) only within same account.
                            continue;
                        }
                        var addr = accountManager.GetAddress(index, platform);
                        if (!string.IsNullOrEmpty(addr))
                        {
                            var shortenedPlatform = platform.ToString();
                            if (platform.ToString() == "Phantasma")
                            {
                                shortenedPlatform = "Pha";
                            }
                            var key = $"{account.name} [{shortenedPlatform}]";
                            hints[key] = addr;
                        }
                    }
                }
            }

            return hints;
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

        #region UI THREAD UTILS
        private List<Action> _uiCallbacks = new List<Action>();

        public void CallOnUIThread(Action callback)
        {
            lock (_uiCallbacks)
            {
                _uiCallbacks.Add(callback);
            }
        }
        #endregion

        #region DAPP Interface
        public Address GetAddress()
        {
            return Address.FromText(AccountManager.Instance.CurrentState.address);
        }

        public Dictionary<string, decimal>  GetBalances(string chain)
        {
            throw new NotImplementedException();
        }

        public void ExecuteTransaction(string description, byte[] script, string chain, Action<Hash> callback)
        {
            this.SendTransaction(description, script, null, chain, callback);
        }

        public void InvokeScript(string chain, byte[] script, Action<byte[]> callback)
        {
            if (script == null)
            {
                Log.Write($"Error invoking script. Script is null.");
                callback(null);
            }

            var accountManager = AccountManager.Instance;

            accountManager.InvokeScript(chain, script, (result, error) =>
            {
                if (String.IsNullOrEmpty(error))
                {
                    callback(result);
                }
                else
                {
                    Log.Write($"Error invoking script.\n{error}\nScript: {System.Text.Encoding.UTF8.GetString(script)}");
                    callback(null);
                }
            });
        }
        #endregion
    }

}
