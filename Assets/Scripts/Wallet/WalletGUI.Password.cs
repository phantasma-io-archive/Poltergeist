using System;
using UnityEngine;
using Phantasma.SDK;
using Poltergeist.PhantasmaLegacy.Cryptography;

namespace Poltergeist
{
    public partial class WalletGUI : MonoBehaviour
    {
        private string masterPassword;

        private void TryPassword(string password, string description, PlatformKind platforms, bool forcePasswordPrompt, bool allowMasterPasswordPrompt, Action<PromptResult> callback)
        {
            var accountManager = AccountManager.Instance;

            // Checking if we can get correct public key by decrypting WIF with given password.
            string wif;
            try
            {
                AccountManager.GetPasswordHashBySalt(password, accountManager.CurrentAccount.passwordIterations, accountManager.CurrentAccount.salt, out string passwordHash);

                wif = AccountManager.DecryptString(accountManager.CurrentAccount.WIF, passwordHash, accountManager.CurrentAccount.iv);

                if (PhantasmaKeys.FromWIF(wif).Address.ToString() == accountManager.CurrentAccount.phaAddress)
                {
                    accountManager.CurrentPasswordHash = passwordHash;
                    callback(PromptResult.Success);
                }
                else
                {
                    MessageBox(MessageKind.Error, $"Incorrect password for '{accountManager.CurrentAccount.name}' account.", () =>
                    {
                        masterPassword = null;
                        RequestPassword(description, platforms, forcePasswordPrompt, allowMasterPasswordPrompt, callback);
                    });
                }
            }
            catch (Exception e)
            {
                Log.WriteWarning("Authorization error: " + e.ToString());
                MessageBox(MessageKind.Error, $"Incorrect password for '{accountManager.CurrentAccount.name}' account.", () =>
                {
                    masterPassword = null;
                    RequestPassword(description, platforms, forcePasswordPrompt, allowMasterPasswordPrompt, callback);
                });
            }
        }
        public void RequestPassword(string description, PlatformKind platforms, bool forcePasswordPrompt, bool allowMasterPasswordPrompt, Action<PromptResult> callback)
        {
            var accountManager = AccountManager.Instance;

            if (!accountManager.HasSelection)
            {
                callback(PromptResult.Failure);
                return;
            }

            if (!accountManager.CurrentAccount.passwordProtected)
            {
                callback(PromptResult.Success);
                return;
            }

            if (!forcePasswordPrompt && accountManager.Settings.passwordMode == PasswordMode.Ask_Only_On_Login)
            {
                callback(PromptResult.Success);
                return;
            }

            var proceedWithPasswordCheck = new Action(() =>
            {
                if (!string.IsNullOrEmpty(masterPassword))
                {
                    TryPassword(masterPassword, description, platforms, forcePasswordPrompt, allowMasterPasswordPrompt, callback);
                }
                else
                {
                    AudioManager.Instance.PlaySFX("auth");
                    ShowModal("Account Authorization", $"Account: {accountManager.CurrentAccount.name} ({platforms})\nAction: {description}\n\nInsert password to proceed...", ModalState.Password, AccountManager.MinPasswordLength, AccountManager.MaxPasswordLength, ModalConfirmCancel, 1, (result, input) =>
                    {
                        var auth = result;

                        if (auth == PromptResult.Success)
                        {
                            TryPassword(input, description, platforms, forcePasswordPrompt, allowMasterPasswordPrompt, callback);
                        }
                    });
                }
            });

            if (AccountManager.Instance.Settings.passwordMode == PasswordMode.Master_Password &&
                string.IsNullOrEmpty(masterPassword) &&
                allowMasterPasswordPrompt)
            {
                ShowModal("Master Password", "Please enter master password", ModalState.Password, AccountManager.MinPasswordLength, AccountManager.MaxPasswordLength, ModalConfirmCancel, 1, (success, input) =>
                {
                    if (success == PromptResult.Success)
                    {
                        masterPassword = input;
                        proceedWithPasswordCheck();
                    }
                    else
                    {
                        // Master password dialog cancelled, using usual password dialog.
                        RequestPassword(description, platforms, forcePasswordPrompt, false, callback);
                    }
                });
            }
            else
            {
                proceedWithPasswordCheck();
            }
        }
    }
}
