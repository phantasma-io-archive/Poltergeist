using System;
using System.Linq;
using UnityEngine;
using Phantasma.SDK;
using Poltergeist.PhantasmaLegacy.Numerics;
using Poltergeist.PhantasmaLegacy.VM.Utils;
using Poltergeist.PhantasmaLegacy.VM;
using System.Numerics;

namespace Poltergeist
{
    public partial class WalletGUI : MonoBehaviour
    {
        private int currencyIndex;
        private string[] currencyOptions;
        private ComboBox currencyComboBox = new ComboBox();

        private int nexusIndex;
        private ComboBox nexusComboBox = new ComboBox();

        private NexusKind[] availableNexus = Enum.GetValues(typeof(NexusKind)).Cast<NexusKind>().ToArray();

        private int mnemonicPhraseLengthIndex;
        private ComboBox mnemonicPhraseLengthComboBox = new ComboBox();

        private MnemonicPhraseLength[] availableMnemonicPhraseLengths = Enum.GetValues(typeof(MnemonicPhraseLength)).Cast<MnemonicPhraseLength>().ToArray();

        private int mnemonicPhraseVerificationModeIndex;
        private ComboBox mnemonicPhraseVerificationModeComboBox = new ComboBox();

        private MnemonicPhraseVerificationMode[] availableMnemonicPhraseVerificationModes = Enum.GetValues(typeof(MnemonicPhraseVerificationMode)).Cast<MnemonicPhraseVerificationMode>().ToArray();

        private int passwordModeIndex;
        private ComboBox passwordModeComboBox = new ComboBox();

        private PasswordMode[] availablePasswordModes = Enum.GetValues(typeof(PasswordMode)).Cast<PasswordMode>().ToArray();


        private int ethereumNetworkIndex;
        private ComboBox ethereumNetworkComboBox = new ComboBox();

        private EthereumNetwork[] availableEthereumNetworks = Enum.GetValues(typeof(EthereumNetwork)).Cast<EthereumNetwork>().ToArray();


        private int binanceSmartChainNetworkIndex;
        private ComboBox binanceSmartChainNetworkComboBox = new ComboBox();

        private BinanceSmartChainNetwork[] availableBinanceSmartChainNetworks = Enum.GetValues(typeof(BinanceSmartChainNetwork)).Cast<BinanceSmartChainNetwork>().ToArray();


        private int logLevelIndex;
        private ComboBox logLevelComboBox = new ComboBox();

        private int uiThemeIndex;
        private ComboBox uiThemeComboBox = new ComboBox();

        private Log.Level[] availableLogLevels = Enum.GetValues(typeof(Log.Level)).Cast<Log.Level>().ToArray();

        private UiThemes[] availableUiThemes = Enum.GetValues(typeof(UiThemes)).Cast<UiThemes>().ToArray();

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
            int boxHeight = (int)(windowRect.height - curY);
            
            fieldWidth = Math.Min(fieldWidth, boxWidth - fieldX - Units(3));
            comboWidth = Math.Min(comboWidth, boxWidth - fieldX - Units(3));

            GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "");

            // Height calculation:
            // 1) 27 elements with total height of (element height + spacing) * 27 = Units(3) * 27.
            // 2) Dropdown space for log level combo: Units(2) * 3.
            // 3) Last element has additional Units(1) spacing before it.
            int elementsNumber;
            switch(settings.nexusKind)
            {
                case NexusKind.Main_Net:
                    elementsNumber = 23;
                    break;
                case NexusKind.Test_Net:
                    elementsNumber = VerticalLayout ? 27 : 26;
                    break;
                case NexusKind.Mankini_Test_Net:
                    elementsNumber = VerticalLayout ? 25 : 24;
                    break;
                case NexusKind.Local_Net:
                    elementsNumber = VerticalLayout ? 33 : 32;
                    break;
                default:
                    elementsNumber = 32;
                    break;
            }
            var insideRect = new Rect(0, 0, boxWidth, Units(3) * elementsNumber + Units(2) * 3 + Units(1));
            // Height calculation: Units(4) space in the bottom of box is occupied by buttons row.
            var outsideRect = new Rect(startX, startY, boxWidth, boxHeight - ((VerticalLayout) ? Units(10) : Units(4)));

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

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Seed length");
            var mnemonicPhraseLengthsList = availableMnemonicPhraseLengths.Select(x => x.ToString().Replace('_', ' ')).ToArray();
            mnemonicPhraseLengthIndex = mnemonicPhraseLengthComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), mnemonicPhraseLengthsList, 0, out dropHeight, null, 0);
            settings.mnemonicPhraseLength = availableMnemonicPhraseLengths[mnemonicPhraseLengthIndex];
            curY += dropHeight + Units(1);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Seed verification");
            var mnemonicPhraseVerificationModesList = availableMnemonicPhraseVerificationModes.Select(x => x.ToString().Replace('_', ' ')).ToArray();
            mnemonicPhraseVerificationModeIndex = mnemonicPhraseVerificationModeComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), mnemonicPhraseVerificationModesList, 0, out dropHeight, null, 0);
            settings.mnemonicPhraseVerificationMode = availableMnemonicPhraseVerificationModes[mnemonicPhraseVerificationModeIndex];
            curY += dropHeight + Units(1);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Password mode");
            var passwordModesList = availablePasswordModes.Select(x => x.ToString().Replace('_', ' ')).ToArray();
            var prevPasswordModeIndex = passwordModeIndex;
            passwordModeIndex = passwordModeComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), passwordModesList, 0, out dropHeight, null, 0);
            settings.passwordMode = availablePasswordModes[passwordModeIndex];
            curY += dropHeight + Units(1);

            if (prevPasswordModeIndex != passwordModeIndex)
            {
                // Password mode is changed.
                masterPassword = null;
            }

            bool hasCustomEndPoints = false;
            bool hasCustomFee = false;
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

                case NexusKind.Test_Net:
                    {
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
                settings.phantasmaRPCURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.phantasmaRPCURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Phantasma Explorer URL");
                settings.phantasmaExplorer = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.phantasmaExplorer);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Phantasma NFT URL");
                settings.phantasmaNftExplorer = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.phantasmaNftExplorer);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Neo RPC URL");
                settings.neoRPCURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.neoRPCURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Neoscan API URL");
                settings.neoscanURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.neoscanURL);
                curY += Units(3);

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Ethereum network");
                var ethereumNetworkList = availableEthereumNetworks.Select(x => x.ToString().Replace('_', ' ')).ToArray();
                var prevEthereumNetworkNexus = ethereumNetworkIndex;
                ethereumNetworkIndex = ethereumNetworkComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), ethereumNetworkList, 0, out dropHeight, null, 1);
                settings.ethereumNetwork = availableEthereumNetworks[ethereumNetworkIndex];
                curY += dropHeight + Units(1);

                if (prevEthereumNetworkNexus != ethereumNetworkIndex)
                {
                    settings.RestoreEthereumEndpoint();
                }

                if (settings.ethereumNetwork == EthereumNetwork.Local_Net)
                {
                    GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Ethereum RPC URL");
                    settings.ethereumRPCURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.ethereumRPCURL);
                    curY += Units(3);
                }


                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "BSC network");
                var binanceSmartChainNetworkList = availableBinanceSmartChainNetworks.Select(x => x.ToString().Replace('_', ' ')).ToArray();
                var prevBinanceSmartChainNetworkNexus = binanceSmartChainNetworkIndex;
                binanceSmartChainNetworkIndex = binanceSmartChainNetworkComboBox.Show(new Rect(fieldComboX, curY, comboWidth, Units(2)), binanceSmartChainNetworkList, 0, out dropHeight, null, 1);
                settings.binanceSmartChainNetwork = availableBinanceSmartChainNetworks[binanceSmartChainNetworkIndex];
                curY += dropHeight + Units(1);

                if (prevBinanceSmartChainNetworkNexus != binanceSmartChainNetworkIndex)
                {
                    settings.RestoreBinanceSmartChainEndpoint();
                }

                if (settings.binanceSmartChainNetwork == BinanceSmartChainNetwork.Local_Net)
                {
                    GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "BSC RPC URL");
                    settings.binanceSmartChainRPCURL = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.binanceSmartChainRPCURL);
                    curY += Units(3);
                }
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

                GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "Phantasma fee limit");
                var limit = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.feeLimit.ToString());
                BigInteger.TryParse(limit, out settings.feeLimit);
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

            // BinanceSmartChain fees, should be editable in all modes.

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "BSC gas price (Gwei)");
            var binanceSmartChainGasPriceGwei = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.binanceSmartChainGasPriceGwei.ToString());
            BigInteger.TryParse(binanceSmartChainGasPriceGwei, out settings.binanceSmartChainGasPriceGwei);
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "BSC transfer gas limit");
            var binanceSmartChainTransactionGasLimit = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.binanceSmartChainTransferGasLimit.ToString());
            BigInteger.TryParse(binanceSmartChainTransactionGasLimit, out settings.binanceSmartChainTransferGasLimit);
            curY += Units(3);

            GUI.Label(new Rect(posX, curY, labelWidth, labelHeight), "BSC token tr. gas limit");
            var binanceSmartChainTokenTransactionGasLimit = GUI.TextField(new Rect(fieldX, curY, fieldWidth, Units(2)), settings.binanceSmartChainTokenTransferGasLimit.ToString());
            BigInteger.TryParse(binanceSmartChainTokenTransactionGasLimit, out settings.binanceSmartChainTokenTransferGasLimit);
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


            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Add token", () =>
            {
                PromptBox("Please select token's blockchain", ModalNeoEthereum, (blockchain) =>
                {
                    PlatformKind platform;
                    if (blockchain == PromptResult.Success)
                    {
                        platform = PlatformKind.Neo;
                    }
                    else
                    {
                        platform = PlatformKind.Ethereum;
                    }
                    ShowModal("Token Symbol", "Enter symbol of a token", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result, tokenSymbol) =>
                    {
                        if (result == PromptResult.Success)
                        {
                            AudioManager.Instance.PlaySFX("click");

                            ShowModal("Token Name", "Enter name of a token", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result2, tokenName) =>
                            {
                                if (result2 == PromptResult.Success)
                                {
                                    AudioManager.Instance.PlaySFX("click");

                                    ShowModal("Token Decimals", "Enter decimals of a token", ModalState.Input, 1, -1, ModalConfirmCancel, 1, (result3, tokenDecimals) =>
                                    {
                                        if (result3 == PromptResult.Success)
                                        {
                                            AudioManager.Instance.PlaySFX("click");

                                            try
                                            {
                                                Int32.Parse(tokenDecimals);
                                            }
                                            catch(Exception)
                                            {
                                                MessageBox(MessageKind.Error, "Invalid decimals!");
                                                return;
                                            }

                                            ShowModal("Token Hash", "Enter hash of a token (without 0x prefix)", ModalState.Input, 40, 42, ModalConfirmCancel, 1, (result4, tokenHash) =>
                                            {
                                                if (result4 == PromptResult.Success)
                                                {
                                                    AudioManager.Instance.PlaySFX("click");

                                                    if (tokenHash.StartsWith("0x"))
                                                        tokenHash = tokenHash.Substring(2);

                                                    ShowModal("Token CoinGecko identifier", "Enter id of a token (you can leave it blank, token price won't be available)", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result5, coinGeckoId) =>
                                                    {
                                                        if (result5 == PromptResult.Success)
                                                        {
                                                            AudioManager.Instance.PlaySFX("click");
                                                            Tokens.UserTokenAdd(platform, tokenSymbol, tokenName, Int32.Parse(tokenDecimals), tokenHash, coinGeckoId);

                                                            MessageBox(MessageKind.Default, "Token successfully added!");
                                                        }
                                                    });
                                                }
                                            });
                                        }
                                    });
                                }
                            });
                        }
                    });
                });
            });
            curY += Units(3);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Edit token", () =>
            {
                PromptBox("Please select token's blockchain", ModalNeoEthereum, (blockchain) =>
                {
                    PlatformKind platform;
                    if (blockchain == PromptResult.Success)
                    {
                        platform = PlatformKind.Neo;
                    }
                    else
                    {
                        platform = PlatformKind.Ethereum;
                    }
                    ShowModal("Token Symbol", "Enter symbol of a token", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result, tokenSymbol) =>
                    {
                        if (result == PromptResult.Success)
                        {
                            AudioManager.Instance.PlaySFX("click");

                            ShowModal("Token Name", "Enter name of a token", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result2, tokenName) =>
                            {
                                if (result2 == PromptResult.Success)
                                {
                                    AudioManager.Instance.PlaySFX("click");

                                    ShowModal("Token Decimals", "Enter decimals of a token", ModalState.Input, 1, -1, ModalConfirmCancel, 1, (result3, tokenDecimals) =>
                                    {
                                        if (result3 == PromptResult.Success)
                                        {
                                            AudioManager.Instance.PlaySFX("click");

                                            try
                                            {
                                                Int32.Parse(tokenDecimals);
                                            }
                                            catch (Exception)
                                            {
                                                MessageBox(MessageKind.Error, "Invalid decimals!");
                                                return;
                                            }

                                            ShowModal("Token Hash", "Enter hash of a token (without 0x prefix)", ModalState.Input, 40, 42, ModalConfirmCancel, 1, (result4, tokenHash) =>
                                            {
                                                if (result4 == PromptResult.Success)
                                                {
                                                    AudioManager.Instance.PlaySFX("click");

                                                    if (tokenHash.StartsWith("0x"))
                                                        tokenHash = tokenHash.Substring(2);

                                                    ShowModal("Token CoinGecko identifier", "Enter id of a token (you can leave it blank, token price won't be available)", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result5, coinGeckoId) =>
                                                    {
                                                        if (result5 == PromptResult.Success)
                                                        {
                                                            AudioManager.Instance.PlaySFX("click");
                                                            if (Tokens.UserTokenEdit(platform, tokenSymbol, tokenName, Int32.Parse(tokenDecimals), tokenHash, coinGeckoId))
                                                            {
                                                                MessageBox(MessageKind.Default, "Token successfully edited!");
                                                            }
                                                            else
                                                            {
                                                                MessageBox(MessageKind.Default, "Token editing failed!");
                                                            }
                                                        }
                                                    });
                                                }
                                            });
                                        }
                                    });
                                }
                            });
                        }
                    });
                });
            });
            curY += Units(3);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Delete token", () =>
            {
                PromptBox("Please select token's blockchain", ModalNeoEthereum, (blockchain) =>
                {
                    PlatformKind platform;
                    if (blockchain == PromptResult.Success)
                    {
                        platform = PlatformKind.Neo;
                    }
                    else
                    {
                        platform = PlatformKind.Ethereum;
                    }
                    ShowModal("Token Symbol", "Enter symbol of a token", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result, tokenSymbol) =>
                    {
                        if (result == PromptResult.Success)
                        {
                            AudioManager.Instance.PlaySFX("click");

                            PromptBox($"Are you sure you want to delete token {tokenSymbol.ToUpper()} [{platform}]?", ModalConfirmCancel, (deleteResult) =>
                            {
                                if (deleteResult == PromptResult.Success)
                                {
                                    AudioManager.Instance.PlaySFX("click");

                                    if (Tokens.UserTokenDelete(platform, tokenSymbol))
                                    {
                                        MessageBox(MessageKind.Default, "Token successfully deleted!");
                                    }
                                    else
                                    {
                                        MessageBox(MessageKind.Default, "Token deletion failed!");
                                    }
                                }
                            });
                        }
                    });
                });
            });
            curY += Units(3);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Delete all tokens", () =>
            {
                PromptBox($"Are you sure you want to delete all user tokens for Ethereum and Neo?", ModalConfirmCancel, (deleteResult) =>
                {
                    if (deleteResult == PromptResult.Success)
                    {
                        AudioManager.Instance.PlaySFX("click");

                        Tokens.UserTokensDeleteAll();
                        MessageBox(MessageKind.Default, "Tokens successfully deleted!");
                    }
                });
            });
            curY += Units(3);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Export tokens", () =>
            {
                PromptBox("Please select tokens' blockchain", ModalNeoEthereum, (blockchain) =>
                {
                    PlatformKind platform;
                    if (blockchain == PromptResult.Success)
                    {
                        platform = PlatformKind.Neo;
                    }
                    else
                    {
                        platform = PlatformKind.Ethereum;
                    }

                    ShowModal("Tokens Export", $"Copy tokens export data to the clipboard?",
                        ModalState.Message, 0, 0, ModalConfirmCancel, 0, (result, input) =>
                    {
                        AudioManager.Instance.PlaySFX("click");

                        if (result == PromptResult.Success)
                        {
                            GUIUtility.systemCopyBuffer = Tokens.UserTokensGet(platform);
                            MessageBox(MessageKind.Default, "Tokens export data copied to the clipboard.");
                        }
                    });
                });
            });
            curY += Units(3);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Import tokens", () =>
            {
                PromptBox("Please select tokens' blockchain", ModalNeoEthereum, (blockchain) =>
                {
                    PlatformKind platform;
                    if (blockchain == PromptResult.Success)
                    {
                        platform = PlatformKind.Neo;
                    }
                    else
                    {
                        platform = PlatformKind.Ethereum;
                    }

                    ShowModal("Tokens Import", "Please enter tokens data that you received from Tokens Export dialog:", ModalState.Input, 1, -1, ModalConfirmCancel, 4, (result, tokensData) =>
                    {
                        AudioManager.Instance.PlaySFX("click");

                        if (result == PromptResult.Success)
                        {
                            if (Tokens.UserTokensSet(platform, tokensData))
                            {
                                MessageBox(MessageKind.Default, "Tokens successfully imported.");
                            }
                            else
                            {
                                MessageBox(MessageKind.Default, "Tokens cannot be imported.");
                            }
                        }
                    });
                });
            });
            curY += Units(3);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Phantasma staking info", () =>
            {
                byte[] scriptMasterClaimDate;
                byte[] scriptMasterCount;
                byte[] scriptClaimMasterCount;
                byte[] scriptMasterThreshold;
                try
                {
                    {
                        var sb = new ScriptBuilder();
                        sb.CallContract("stake", "GetMasterClaimDate", 1);
                        scriptMasterClaimDate = sb.EndScript();
                    }
                    {
                        var sb = new ScriptBuilder();
                        sb.CallContract("stake", "GetMasterCount");
                        scriptMasterCount = sb.EndScript();
                    }
                    {
                        var sb = new ScriptBuilder();
                        sb.CallContract("stake", "GetMasterThreshold");
                        scriptMasterThreshold = sb.EndScript();
                    }
                }
                catch (Exception e)
                {
                    MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message + "\n\n" + e.StackTrace);
                    return;
                }

                accountManager.InvokeScriptPhantasma("main", scriptMasterClaimDate, (masterClaimDateResult, masterClaimInvokeError) =>
                {
                    if(!string.IsNullOrEmpty(masterClaimInvokeError))
                    {
                        MessageBox(MessageKind.Error, "Script invokation error!\n\n" + masterClaimInvokeError);
                        return;
                    }
                    else
                    {
                        {
                            var sb = new ScriptBuilder();
                            sb.CallContract("stake", "GetClaimMasterCount", VMObject.FromBytes(masterClaimDateResult).AsTimestamp());
                            scriptClaimMasterCount = sb.EndScript();
                        }

                        accountManager.InvokeScriptPhantasma("main", scriptClaimMasterCount, (claimMasterCountResult, claimMasterCountInvokeError) =>
                        {
                            if (!string.IsNullOrEmpty(claimMasterCountInvokeError))
                            {
                                MessageBox(MessageKind.Error, "Script invokation error!\n\n" + claimMasterCountInvokeError);
                                return;
                            }
                            else
                            {
                                accountManager.InvokeScriptPhantasma("main", scriptMasterCount, (masterCountResult, masterCountInvokeError) =>
                                {
                                    if (!string.IsNullOrEmpty(masterCountInvokeError))
                                    {
                                        MessageBox(MessageKind.Error, "Script invokation error!\n\n" + masterCountInvokeError);
                                        return;
                                    }
                                    else
                                    {
                                        accountManager.InvokeScriptPhantasma("main", scriptMasterThreshold, (masterThresholdResult, masterThresholdInvokeError) =>
                                        {
                                            if (!string.IsNullOrEmpty(masterThresholdInvokeError))
                                            {
                                                MessageBox(MessageKind.Error, "Script invokation error!\n\n" + masterThresholdInvokeError);
                                                return;
                                            }
                                            else
                                            {
                                                var masterClaimDate = VMObject.FromBytes(masterClaimDateResult).AsTimestamp();
                                                var claimMasterCount = VMObject.FromBytes(claimMasterCountResult).AsNumber();
                                                var masterCount = VMObject.FromBytes(masterCountResult).AsNumber();
                                                var masterThreshold = UnitConversion.ToDecimal(VMObject.FromBytes(masterThresholdResult).AsNumber(), 8);

                                                ShowModal("Account information",
                                                    $"Phantasma staking information:\n\n" +
                                                    $"All SMs: {masterCount}\n" +
                                                    $"SMs eligible for next rewards distribution: {claimMasterCount}\n" +
                                                    $"SM reward prediction: {125000/claimMasterCount} SOUL\n" +
                                                    $"Next SM rewards distribution date: {masterClaimDate}\n" +
                                                    $"SM threshold: {masterThreshold} SOUL\n",
                                                    ModalState.Message, 0, 0, ModalOkCopy, 0, (result, input) => { });
                                            }
                                        });
                                    }
                                });
                            }
                        });
                    }
                            
                });
            });
            curY += Units(3);

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Phantasma address info", () =>
            {
                ShowModal("Address", "Enter an address", ModalState.Input, 2, -1, ModalConfirmCancel, 1, (result, input) =>
                {
                    if (result == PromptResult.Success)
                    {
                        accountManager.GetPhantasmaAddressInfo(input, (result2, error) =>
                        {
                            if (!string.IsNullOrEmpty(error))
                            {
                                MessageBox(MessageKind.Error, "Something went wrong!\n" + error);
                                return;
                            }
                            else
                            {
                                ShowModal("Account information", result2,
                                    ModalState.Message, 0, 0, ModalOkCopy, 0, (result3, input3) => { });
                                return;
                            }
                        });
                    }
                });
            });
            curY += Units(3);

            curY += Units(1);
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

            DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Reset settings", () =>
            {
                PromptBox("All settings will be set to default values.\nMake sure you have backups of your private keys!", ModalConfirmCancel, (result) =>
                {
                    if (result == PromptResult.Success)
                    {
                        AudioManager.Instance.PlaySFX("click");

                        // Saving wallets before settings reset.
                        var walletsVersion = PlayerPrefs.GetInt(AccountManager.WalletVersionTag);
                        var wallets = PlayerPrefs.GetString(AccountManager.WalletTag, "");
                        // TODO: Remove before release.
                        var walletsLegacy = PlayerPrefs.GetString(AccountManager.WalletLegacyTag, "");

                        PlayerPrefs.DeleteAll();

                        // Restoring wallets before settings reset.
                        PlayerPrefs.SetInt(AccountManager.WalletVersionTag, walletsVersion);
                        PlayerPrefs.SetString(AccountManager.WalletTag, wallets);
                        // TODO: Remove before release.
                        PlayerPrefs.SetString(AccountManager.WalletLegacyTag, walletsLegacy);

                        // Loading default settings.
                        accountManager.Settings.Load();

                        // Finding fastest Phantasma and Neo RPCs.
                        accountManager.UpdateRPCURL(PlatformKind.Phantasma);
                        accountManager.UpdateRPCURL(PlatformKind.Neo);
                        accountManager.UpdateRPCURL(PlatformKind.BSC);

                        // Restoring combos' selected items.
                        // If they are not restored, following calls of DoSettingsScreen() will change them again.
                        SetState(GUIState.Settings);

                        MessageBox(MessageKind.Default, "All settings set to default values.", () =>
                        {
                            CloseCurrentStack();
                        });
                    }
                }, 0);
            });
            curY += Units(3);

            if (accountManager.Accounts.Count() > 0)
            {
                curY += Units(1);
                DoButton(true, new Rect(posX, curY, Units(16), Units(2)), "Delete everything", () =>
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
                    }, 10);
                });

                curY += Units(3);
            }
            
            GUI.EndScrollView();

            var btnWidth = Units(10);
            var btnHeight = Units(2);
            var btnVerticalSpacing = 4;
            curY = (int)(windowRect.height - Units(4));

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

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            string[] settingsMenu = new string[] { "Display settings", "Open log location", "Cancel", "Confirm" };
#else
            string[] settingsMenu = new string[] { "Display settings", "Show log location", "Cancel", "Confirm" };
#endif
            int posY;
            DoButtonGrid<int>(false, settingsMenu.Length, (VerticalLayout) ? 0 : Units(2), 0, out posY, (index) =>
            {
                return new MenuEntry(index, settingsMenu[index], true);
            },
            (selected) =>
            {
                switch (selected)
                {
                    case 0:
                        {
                            var currentSettings = accountManager.Settings.ToString();
                            ShowModal("Display Settings",
                                currentSettings,
                                ModalState.Message, 0, 0, ModalOkCopy, 0, (result, input) =>
                                {
                                    if (result == PromptResult.Failure)
                                    {
                                        AudioManager.Instance.PlaySFX("click");
                                        GUIUtility.systemCopyBuffer = currentSettings;
                                    }
                                });

                            break;
                        }
                    case 1:
                        {
                            AudioManager.Instance.PlaySFX("click");
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                            string path = System.IO.Path.GetDirectoryName(Log.FilePath).TrimEnd(new[] { '\\', '/' }); // Mac doesn't like trailing slash
                            System.Diagnostics.Process.Start(path);
#else
                            ShowModal("Log file path",
                                Log.FilePath,
                                ModalState.Message, 0, 0, ModalOkCopy, 0, (result, input) =>
                                {
                                    if (result == PromptResult.Failure)
                                    {
                                        AudioManager.Instance.PlaySFX("click");
                                        GUIUtility.systemCopyBuffer = Log.FilePath;
                                    }
                                });
#endif
                            break;
                        }

                    case 2:
                        {
                            AudioManager.Instance.PlaySFX("cancel");

                            // Resetting changes by restoring current settings.
                            settings.Load();

                            // Restoring combos' selected items.
                            // If they are not restored, following calls of DoSettingsScreen() will change them again.
                            SetState(GUIState.Settings);

                            CloseCurrentStack();
                            break;
                        }

                    case 3:
                        {
                            if (ValidateSettings())
                            {
                                AudioManager.Instance.PlaySFX("confirm");
                                ResourceManager.Instance.UnloadTokens();
                                CloseCurrentStack();
                            }
                            break;
                        }
                }
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

            if (!settings.phantasmaExplorer.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma Explorer URL.\n" + settings.phantasmaExplorer);
                return false;
            }

            if (!settings.phantasmaNftExplorer.IsValidURL())
            {
                MessageBox(MessageKind.Error, "Invalid URL for Phantasma NFT Explorer URL.\n" + settings.phantasmaNftExplorer);
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

            if (settings.feeLimit < 900)
            {
                MessageBox(MessageKind.Error, "Invalid value for fee limit.\n" + settings.feeLimit);
                return false;
            }

            if (accountManager.Accounts.Count() == 0)
            {
                accountManager.InitDemoAccounts(settings.nexusKind);
            }

            accountManager.UpdateRPCURL(PlatformKind.Phantasma);
            accountManager.UpdateRPCURL(PlatformKind.Neo);
            accountManager.UpdateRPCURL(PlatformKind.BSC);

            accountManager.UpdateAPIs(true);
            accountManager.RefreshTokenPrices();
            accountManager.Settings.Save();
            return true;
        }
    }
}
