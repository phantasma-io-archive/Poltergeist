using UnityEngine;
using System.IO;
using System;
using Phantasma.SDK;
using System.Linq;
#if CT_FB && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
using Crosstales.FB;
#elif UNITY_ANDROID
using static NativeFilePicker;
#endif
using Archive = Phantasma.SDK.Archive;
using System.Text;
using Phantasma.Core.Domain;
using Phantasma.Core.Cryptography;
using Phantasma.Business.VM.Utils;
using Phantasma.Business.Blockchain.Storage;

namespace Poltergeist
{
    public partial class WalletGUI : MonoBehaviour
    {
        private void UploadSelectedFile(string targetFilePath)
        {
            var accountManager = AccountManager.Instance;

            if (!string.IsNullOrEmpty(targetFilePath))
            {
                if (File.Exists(targetFilePath))
                {
                    accountManager.Settings.SetLastVisitedFolder(Path.GetDirectoryName(targetFilePath));

                    var extension = Path.GetExtension(targetFilePath);

                    switch (extension)
                    {
                        case ".pvm":
                            PromptBox("This file is a contract. Deploy it?", ModalYesNo, (encryptFile) =>
                            {
                                var abiFile = targetFilePath.Replace(".pvm", ".abi");
                                if (File.Exists(abiFile))
                                {
                                    DeployContract(targetFilePath, abiFile);
                                }
                                else
                                {
                                    MessageBox(MessageKind.Error, $"The ABI file for this contract was not found.");
                                }
                            });
                            break;

                        default:
                            var size = (int)(new System.IO.FileInfo(targetFilePath).Length);

                            if (size < DomainSettings.ArchiveMinSize)
                            {
                                MessageBox(MessageKind.Error, $"File is too small to upload.\nMinimum allowed size is {DomainSettings.ArchiveMinSize} bytes.");
                            }
                            else
                            {
                                if (size > DomainSettings.ArchiveMaxSize)
                                {
                                    MessageBox(MessageKind.Error, $"File is too big to upload.\nMaximum allowed size is {DomainSettings.ArchiveMaxSize} bytes ({(DomainSettings.ArchiveMaxSize / (double)Math.Pow(1024, 2)).ToString("0.00")} MB).");
                                }
                                else
                                {
                                    RequireStorage(size, (sucess) =>
                                    {
                                        if (sucess)
                                        {
                                            PromptBox("Protect this file with encryption?\nIf you choose 'Yes' this file would be protected and you would be the only person able to open it.\nIf you choose 'No', anyone would be able to open it.", ModalYesNo, (encryptFile) =>
                                            {
                                                var content = File.ReadAllBytes(targetFilePath);
                                                UploadArchive(targetFilePath, content, (encryptFile == PromptResult.Success));
                                            });
                                        }

                                    });
                                }
                            }
                            break;

                    }

                }
                else
                {
                    MessageBox(MessageKind.Error, "File not found");
                }
            }
        }
        private void DoStorageScreen()
        {
            var accountManager = AccountManager.Instance;

            int curY = Units(5);

            int startY = curY;
            int endY = (int)(windowRect.yMax - Units(4));

            DoScrollArea<Archive>(ref balanceScroll, startY, endY, VerticalLayout ? Units(6) : Units(4), accountManager.CurrentState.archives, DoStorageEntry);

            int posY;
            DoButtonGrid<int>(false, storageMenu.Length, 0, 0, out posY, (index) =>
            {
                return new MenuEntry(index, storageMenu[index], true);
            },
            (selected) =>
            {
                switch (selected)
                {
                    case 0:
                        {
#if CT_FB && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
                            UploadSelectedFile(FileBrowser.Instance.OpenSingleFile("Open File", accountManager.Settings.GetLastVisitedFolder(), null, new string[] { "*" }));
#elif UNITY_ANDROID
                            var extensionFilter = new string[] {"audio/*", "video/*", "image/*", "text/*", "application/*"};
//#else // iOS
//                            var extensionFilter = new string[] {"public.audiovisual-content", "public.image", "public.text", "public.archive"};
//#endif
                            NativeFilePicker.PickFile((path) => { UploadSelectedFile(path); }, extensionFilter);
#endif

                            break;
                        }
                    case 1:
                        {
                            PopState();
                            break;
                        }
                }
            });
        }

        private void DoStorageEntry(Archive entry, int index, int curY, Rect rect)
        {
            var accountManager = AccountManager.Instance;

            if (entry.encryption != null)
                entry.name = entry.encryption.DecryptName(entry.name, PhantasmaKeys.FromWIF(accountManager.CurrentWif));

            GUI.Label(new Rect(Units(2), curY + 12, Units(20), Units(2) + 4), entry.name);

            var style = GUI.skin.label;
            style.fontSize -= VerticalLayout ? 2 : 0;
            GUI.Label(VerticalLayout ? new Rect(Units(2), curY + Units(3), Units(20), Units(2) + 4) : new Rect(Units(26), curY + 12, Units(20), Units(2) + 4),
                BytesToString(entry.size));
            style.fontSize += VerticalLayout ? 2 : 0;

            if (entry.encryption != null)
            {
                GUI.DrawTexture(new Rect(rect.x + rect.width - Units(17) - 8, curY + (VerticalLayout ? Units(3) : Units(1)), Units(2), Units(2)), lockTexture);
            }

            var btnRect = new Rect(rect.x + rect.width - Units(15), curY + (VerticalLayout ? Units(3) : Units(1)), Units(6), Units(2));
            var btnRect2 = new Rect(rect.x + rect.width - Units(8), curY + (VerticalLayout ? Units(3) : Units(1)), Units(6), Units(2));

            DoButton(true, btnRect, "Download", () =>
            {
#if CT_FB && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
                var outputFolderPath = FileBrowser.Instance.OpenSingleFolder("Select output folder", accountManager.Settings.GetLastVisitedFolder());

                if (!string.IsNullOrEmpty(outputFolderPath))
                {
                    if (Directory.Exists(outputFolderPath))
                    {
                        accountManager.Settings.SetLastVisitedFolder(outputFolderPath);

                        if (!string.IsNullOrEmpty(outputFolderPath))
                            DownloadArchive(Hash.Parse(entry.hash), outputFolderPath);
                    }
                    else
                    {
                        MessageBox(MessageKind.Error, "Folder not found");
                    }
                }
#else
                var outputFolderPath = Path.Combine(Application.persistentDataPath, "Downloads");
                System.IO.Directory.CreateDirectory(outputFolderPath);
                DownloadArchive(Hash.Parse(entry.hash), outputFolderPath);                
#endif
            });

            DoButton(true, btnRect2, "Delete", () =>
            {
                DeleteArchive(entry.name, entry.size, Hash.Parse(entry.hash));
            });
        }

        private void DeleteArchive(string fileName, uint size, Hash fileHash)
        {
            var accountManager = AccountManager.Instance;

            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Phantasma)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Phantasma);
                return;
            }

            var source = Address.FromText(state.address);

            byte[] script;

            try
            {
                var sb = new ScriptBuilder();
                sb.AllowGas(source, Address.Null, accountManager.Settings.feePrice, accountManager.Settings.feeLimit);
                sb.CallContract(NativeContractKind.Storage, "DeleteFile", source, fileHash);
                sb.SpendGas(source);
                script = sb.EndScript();
            }
            catch (Exception e)
            {
                MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message + "\n\n" + e.StackTrace);
                return;
            }

            SendTransaction($"Deleting file '{fileName}'.\nSize: {BytesToString(size)}", script, null, accountManager.Settings.feePrice, accountManager.Settings.feeLimit, null, DomainSettings.RootChainName, ProofOfWork.None, (hash, error) =>
            {
                TxResultMessage(hash, error, null, $"The archive '{fileName}' was deleted!");
            });
        }

        private void DeployContract(string scriptPath, string abiPath)
        {
            var accountManager = AccountManager.Instance;

            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Phantasma)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Phantasma);
                return;
            }

            var contractBytes = File.ReadAllBytes(scriptPath);
            var abiBytes = File.ReadAllBytes(abiPath);

            var target = Address.FromText(state.address);
            var contractName = Path.GetFileNameWithoutExtension(scriptPath);

            byte[] script;
            try
            {
                var gasPrice = accountManager.Settings.feePrice;
                var gasLimit = accountManager.Settings.feeLimit;
                
                var sb = new ScriptBuilder();
                sb.AllowGas(target, Address.Null, gasPrice, gasLimit);
                sb.CallInterop("Runtime.DeployContract", target, contractName, contractBytes, abiBytes);
                sb.SpendGas(target);
                script = sb.EndScript();
            }
            catch (Exception e)
            {
                MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message + "\n\n" + e.StackTrace);
                return;
            }

            SendTransaction($"Uploading contract '{contractName}'.", script, null, accountManager.Settings.feePrice, accountManager.Settings.feeLimit, null, DomainSettings.RootChainName, ProofOfWork.Minimal, (hash, error) =>
            {
                TxResultMessage(hash, error, null, $"{contractName} was deployed successfully!");
            });

        }

        private void UploadArchive(string fileName, byte[] content, bool encrypt)
        {
            var accountManager = AccountManager.Instance;

            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Phantasma)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Phantasma);
                return;
            }

            var target = Address.FromText(state.address);

            var newFileName = Path.GetFileName(fileName);

            byte[] archiveEncryption;

            if (encrypt)
            {
                var privateEncryption = new PrivateArchiveEncryption(Address.FromWIF(accountManager.CurrentWif));
                
                newFileName = privateEncryption.EncryptName(newFileName, PhantasmaKeys.FromWIF(accountManager.CurrentWif));
                
                content = privateEncryption.Encrypt(content, PhantasmaKeys.FromWIF(accountManager.CurrentWif));

                archiveEncryption = privateEncryption.ToBytes();
            }
            else
            {
                archiveEncryption = ArchiveExtensions.Uncompressed;
            }

            var fileSize = content.Length;

            var merkleTree = new MerkleTree(content);
            var merkleBytes = merkleTree.ToByteArray();

            byte[] script;
            try
            {
                var sb = new ScriptBuilder();
                sb.AllowGas(target, Address.Null, accountManager.Settings.feePrice, accountManager.Settings.feeLimit);
                sb.CallContract(NativeContractKind.Storage, "CreateFile", target, newFileName, fileSize, merkleBytes, archiveEncryption);
                sb.SpendGas(target);
                script = sb.EndScript();
            }
            catch (Exception e)
            {
                MessageBox(MessageKind.Error, "Something went wrong!\n" + e.Message + "\n\n" + e.StackTrace);
                return;
            }

            SendTransaction($"Uploading file '{fileName}'.\nSize: {BytesToString(fileSize)}", script, null, accountManager.Settings.feePrice, accountManager.Settings.feeLimit, null, DomainSettings.RootChainName, ProofOfWork.None, (hash, error) =>
            {
                if (string.IsNullOrEmpty(error) && hash != Hash.Null)
                {
                    PushState(GUIState.Upload);

                    _totalUploadChunks = MerkleTree.GetChunkCountForSize((uint)content.Length);
                    UploadChunk(fileName, merkleTree, content, hash, 0);
                }
            });

        }

        private uint _currentUploadChunk;
        private uint _totalUploadChunks;

        private void UploadChunk(string fileName, MerkleTree merkleTree, byte[] content, Hash creationTxHash, int blockIndex)
        {
            _currentUploadChunk = (uint)blockIndex;

            var accountManager = AccountManager.Instance;

            var lastChunk = _totalUploadChunks - 1;

            var isLast = blockIndex == lastChunk;

            var chunkSize = isLast ? content.Length % MerkleTree.ChunkSize : MerkleTree.ChunkSize;
            var chunkData = new byte[chunkSize];

            var offset = blockIndex * MerkleTree.ChunkSize;
            for (int i=0; i<chunkSize; i++)
            {
                chunkData[i] = content[i + offset];
            }

            accountManager.WriteArchive(merkleTree.Root, blockIndex, chunkData, (result, error) =>
            {
                if (result)
                {
                    // if this was the last chunk, show completion msg
                    if (isLast)
                    {
                        PopState();

                        TxResultMessage(creationTxHash, error, $"The archive '{fileName}' was uploaded!");
                    }
                    else
                    {
                        // otherwise upload next chunk
                        UploadChunk(fileName, merkleTree, content, creationTxHash, blockIndex + 1);
                    }
                }
                else
                {
                    PopState();
                    
                    TxResultMessage(creationTxHash, error, null, $"Something went wrong when uploading chunk {blockIndex} for {fileName}!");
                    // TODO allow user to retry ?
                }
                    
            });
        }

        private void DownloadArchive(Hash hash, string outputFolderPath)
        {
            var accountManager = AccountManager.Instance;

            accountManager.GetArchive(hash, (result, archive, error) =>
            {
                if (result)
                {
                    PushState(GUIState.Download);

                    _totalDownloadChunks = archive.blockCount;

                    var name = archive.name;
                    if (archive.encryption != null)
                        name = archive.encryption.DecryptName(archive.name, PhantasmaKeys.FromWIF(accountManager.CurrentWif));

                    DownloadChunk(hash, archive, Path.Combine(outputFolderPath, name), 0);
                }
                else
                {
                    PopState();
                    MessageBox(MessageKind.Error, $"Something went wrong while downloading archive {archive.name}!\nError: " + error);
                }
            });
        }

        private int _currentDownloadChunk;
        private int _totalDownloadChunks;

        private void DownloadChunk(Hash archiveHash, Archive archive, string filePath, int blockIndex)
        {
            _currentDownloadChunk = blockIndex;

            var accountManager = AccountManager.Instance;

            var lastChunk = _totalDownloadChunks - 1;

            var isLast = blockIndex == lastChunk;

            accountManager.ReadArchive(archiveHash, blockIndex, (result, chunkData, error) =>
            {
                if (result)
                {
                    using (var stream = new FileStream(filePath, blockIndex == 0 ? FileMode.Create : FileMode.Append))
                    {
                        stream.Write(chunkData, 0, chunkData.Length);
                    }

                    // if this was the last chunk, decrypt (if encrypted) and show completion msg
                    if (isLast)
                    {
                        if (archive.encryption != null && archive.encryption.Mode == ArchiveEncryptionMode.Private)
                        {
                            var privateEncryption = archive.encryption;

                            var content = File.ReadAllBytes(filePath);
                            content = privateEncryption.Decrypt(content, PhantasmaKeys.FromWIF(accountManager.CurrentWif));

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                stream.Write(content, 0, content.Length);
                            }
                        }

                        PopState();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                        MessageBox(MessageKind.Default, $"The archive '{filePath}' was downloaded!");
#elif UNITY_ANDROID
                        NativeFilePicker.ExportFile(filePath, (success) => 
                            { 
                                if(success)
                                    MessageBox(MessageKind.Default, $"The archive was downloaded!");
                                else
                                    MessageBox(MessageKind.Default, $"Could not download the archive!");
                            });
#endif
                    }
                    else
                    {
                        // otherwise download next chunk
                        DownloadChunk(archiveHash, archive, filePath, blockIndex + 1);
                    }
                }
                else
                {
                    PopState();
                    MessageBox(MessageKind.Error, $"Something went wrong while downloading chunk {blockIndex} for {filePath}!\nError: " + error);
                }
            });
        }

        private void UploadSelectedAvatar(string avatarFilePath)
        {
            var accountManager = AccountManager.Instance;

            if (!string.IsNullOrEmpty(avatarFilePath))
            {
                if (File.Exists(avatarFilePath))
                {
                    accountManager.Settings.SetLastVisitedFolder(Path.GetDirectoryName(avatarFilePath));

                    int expectedSize = 32;

                    var avatarTex = new Texture2D(expectedSize, expectedSize, TextureFormat.RGBA32, false, true);
                    var bytes = File.ReadAllBytes(avatarFilePath);
                    avatarTex.LoadImage(bytes);

                    //avatarTex.Resize(expectedSize, expectedSize); this could be used maybe..

                    if (avatarTex.width != expectedSize || avatarTex.height != expectedSize)
                    {
                        Texture2D.Destroy(avatarTex);
                        MessageBox(MessageKind.Error, $"Avatar picture must have dimensions {expectedSize} x {expectedSize}");
                    }
                    else
                    {
                        var rgbs = avatarTex.GetPixels();
                        bool hasTransparency = false;
                        foreach (var color in rgbs)
                        {
                            if (color.a < 1)
                            {
                                hasTransparency = true;
                                break;
                            }
                        }

                        if (hasTransparency)
                        {
                            MessageBox(MessageKind.Error, "Avatar picture can't have transparent pixels");
                        }
                        else
                        {
                            _promptPicture = avatarTex;
                            PromptBox("Do you want to upload this picture as your account avatar?", ModalYesNo, (wantsUpload) =>
                            {
                                if (wantsUpload == PromptResult.Success)
                                {
                                    var exportedAvatarBytes = avatarTex.EncodeToPNG();

                                    var avatarData = "data:image/png;base64," + System.Convert.ToBase64String(exportedAvatarBytes);

                                    RequireStorage(avatarData.Length, (success) =>
                                    {
                                        var avatarBytes = Encoding.ASCII.GetBytes(avatarData);
                                        UploadArchive("avatar", avatarBytes, false);
                                    });
                                }

                                Texture2D.Destroy(avatarTex);
                            });
                        }

                    }
                }
                else
                {
                    MessageBox(MessageKind.Error, "File not found");
                }
            }
        }

        private void RequireStorage(int bytesRequired, Action<bool> callback)
        {
            var accountManager = AccountManager.Instance;

            var state = accountManager.CurrentState;

            if (accountManager.CurrentPlatform != PlatformKind.Phantasma)
            {
                MessageBox(MessageKind.Error, $"Current platform must be " + PlatformKind.Phantasma);
                return;
            }

            var currentStake = state.balances.Where(x => x.Symbol == DomainSettings.StakingTokenSymbol).Select(x => x.Staked).FirstOrDefault();

            var expectedStake = accountManager.CalculateRequireStakeForStorage(bytesRequired);

            if (currentStake >= expectedStake)
            {
                callback(true);
                return;
            }

            var requiredStake = expectedStake - currentStake;

            StakeSOUL(requiredStake, $"Not enough available storage space to upload.\nStake {requiredStake} {DomainSettings.StakingTokenSymbol} to increase your storage?", (hash, error) =>
            {
                callback(string.IsNullOrEmpty(error) && hash != Hash.Null);
            });
        }
    }
}
