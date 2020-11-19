using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.SDK;
using Phantasma.VM.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Poltergeist
{

    public class DescriptionUtils : MonoBehaviour
    {
        private static bool CheckIfCallShouldBeIgnored(DisasmMethodCall call)
        {
            return ("gas".Equals(call.ContractName) && (call.MethodName == "AllowGas" || call.MethodName == "SpendGas"));
        }

        private static string GetCallFullName(DisasmMethodCall call)
        {
            if (!string.IsNullOrEmpty(call.ContractName))
                return $"{call.ContractName}.{call.MethodName}";
            else
                return call.MethodName;
        }

        private static bool CompareCalls(DisasmMethodCall call1, DisasmMethodCall call2, params int[] argNumbersToCompare)
        {
            // Compare contract and method names.
            if (GetCallFullName(call1) != GetCallFullName(call2) )
                return false;

            for (int i = 0; i < argNumbersToCompare.Length; i++)
            {
                // Compare all arguments as strings.
                if (call1.Arguments[argNumbersToCompare[i]].AsString() != call2.Arguments[argNumbersToCompare[i]].AsString())
                    return false;
            }

            return true;
        }

        private static string GetStringArg(DisasmMethodCall call, int argumentNumber)
        {
            try
            {
                return call.Arguments[argumentNumber].AsString();
            }
            catch(Exception e)
            {
                throw new Exception($"{GetCallFullName(call)}: Error: Cannot get description for argument #{argumentNumber + 1} [String]: {e.Message}");
            }
        }

        private static BigInteger GetNumberArg(DisasmMethodCall call, int argumentNumber)
        {
            try
            {
                return call.Arguments[argumentNumber].AsNumber();
            }
            catch (Exception e)
            {
                throw new Exception($"{GetCallFullName(call)}: Error: Cannot get description for argument #{argumentNumber + 1} [Number]: {e.Message}");
            }
        }

        private static Timestamp GetTimestampArg(DisasmMethodCall call, int argumentNumber)
        {
            try
            {
                return call.Arguments[argumentNumber].AsTimestamp();
            }
            catch (Exception e)
            {
                throw new Exception($"{GetCallFullName(call)}: Error: Cannot get description for argument #{argumentNumber + 1} [Timestamp]: {e.Message}");
            }
        }

        private static byte[] GetByteArrayArg(DisasmMethodCall call, int argumentNumber)
        {
            try
            {
                return call.Arguments[argumentNumber].AsByteArray();
            }
            catch (Exception e)
            {
                throw new Exception($"{GetCallFullName(call)}: Error: Cannot get description for argument #{argumentNumber + 1} [String]: {e.Message}");
            }
        }

        private static List<string> knownContracts = null;
        private static Dictionary<string, int> methodTable = DisasmUtils.GetDefaultDisasmTable();


        public static void RegisterContractMethod(string contractMethod, int paramCount)
        {
            methodTable[contractMethod] = paramCount;
        }

        public static IEnumerator GetDescription(byte[] script, Action<string, string> callback)
        {
            foreach (var entry in methodTable.Keys)
            {
                Debug.Log("disam method: " + entry);
            }

            if(knownContracts == null)
            {
                // Collecting known contract names
                knownContracts = methodTable.Keys.Select(x => x.IndexOf(".") > 0 ? x.Substring(0, x.IndexOf(".")) : x).Distinct().ToList();
            }

            var accountManager = AccountManager.Instance;

            var contracts = DisasmUtils.ExtractContractNames(script).Where(x => !knownContracts.Contains(x));
            var contractsToLoad = contracts.Count();
            var contractsProcessed = 0;
            foreach(var contract in contracts)
            {
                WalletGUI.Instance.StartCoroutine(
                    accountManager.phantasmaApi.GetContract(contract, (contractStruct) =>
                    {
                        Log.Write($"Registering {contractStruct.methods.Length} methods for {contract}");

                        foreach (var method in contractStruct.methods)
                        {
                            Log.Write($"Registering contract method {contract}.{method.name} with {method.parameters.Length} parameters");
                            DescriptionUtils.RegisterContractMethod($"{contract}.{method.name}", method.parameters.Length);
                        }

                        contractsProcessed++;
                    }, (error, msg) =>
                    {
                        Log.WriteWarning("Could not fetch contract info: " + contract);
                        contractsProcessed++;
                    }));
            }

            while (contractsProcessed < contractsToLoad)
            {
                yield return null;
            }

            IEnumerable<DisasmMethodCall> disasm;
            try
            {
                disasm = DisasmUtils.ExtractMethodCalls(script, methodTable);
            }
            catch (Exception e)
            {
                callback(null, e.Message);
                yield break;
            }

            // Checking if all calls are "market.SellToken" calls only or "Runtime.TransferToken" only,
            // and we can group them.
            int groupSize = 0;
            DisasmMethodCall? prevCall = null;
            foreach (var call in disasm)
            {
                if (CheckIfCallShouldBeIgnored(call))
                {
                    continue;
                }

                if (GetCallFullName(call) == "Runtime.TransferToken")
                {
                    if (prevCall != null && !CompareCalls((DisasmMethodCall)prevCall, call, 0, 1))
                    {
                        groupSize = 0;
                        break;
                    }

                    groupSize++;
                }
                else if (GetCallFullName(call) == "market.SellToken")
                {
                    if (prevCall != null && !CompareCalls((DisasmMethodCall)prevCall, call, 0, 2, 4, 5))
                    {
                        groupSize = 0;
                        break;
                    }

                    groupSize++;
                }
                else
                {
                    // Different call, grouping is not supported.
                    groupSize = 0;
                    break;
                }

                prevCall = call;
            }

            int transferTokenCounter = 0; // Counting "market.TransferToken" calls, needed for grouping.
            int sellTokenCounter = 0; // Counting "market.SellToken" calls, needed for grouping.

            var sb = new StringBuilder();
            foreach (var entry in disasm)
            {
                if (CheckIfCallShouldBeIgnored(entry))
                {
                    continue;
                }

                // Put it to log so that developer can easily check what PG is receiving.
                Log.Write("GetDescription(): Contract's description: " + entry.ToString());

                switch (GetCallFullName(entry))
                {
                    case "Runtime.TransferToken":
                        {
                            var src = GetStringArg(entry, 0);
                            var dst = GetStringArg(entry, 1);
                            var symbol = GetStringArg(entry, 2);
                            var nftNumber = GetStringArg(entry, 3);

                            if (groupSize > 1)
                            {
                                if (transferTokenCounter == 0)
                                {
                                    // Desc line #1.
                                    sb.AppendLine("\u2605 Transfer:");
                                }

                                if (transferTokenCounter == groupSize - 1)
                                {
                                    // Desc line for token #N.
                                    sb.AppendLine($"{symbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)}");
                                    sb.AppendLine($"to {dst}.");
                                }
                                else
                                {
                                    // Desc line for tokens #1 ... N-1.
                                    sb.AppendLine($"{symbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)},");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"\u2605 Transfer {symbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)} to {dst}.");
                            }

                            transferTokenCounter++;

                            break;
                        }
                    case "Runtime.TransferTokens":
                        {
                            var src = GetStringArg(entry, 0);
                            var dst = GetStringArg(entry, 1);
                            var symbol = GetStringArg(entry, 2);
                            var amount = GetNumberArg(entry, 3);

                            Token token;
                            accountManager.GetTokenBySymbol(symbol, PlatformKind.Phantasma, out token);

                            var total = UnitConversion.ToDecimal(amount, token.decimals);

                            sb.AppendLine($"\u2605 Transfer {total} {symbol} from {src} to {dst}.");
                            break;
                        }
                    case "market.BuyToken":
                        {
                            var dst = GetStringArg(entry, 0);
                            var symbol = GetStringArg(entry, 1);
                            var nftNumber = GetStringArg(entry, 2);

                            sb.AppendLine($"\u2605 Buy {symbol} NFT #{nftNumber.Substring(0 ,5) + "..." + nftNumber.Substring(nftNumber.Length - 5)}.");
                            break;
                        }
                    case "market.SellToken":
                        {
                            var dst = GetStringArg(entry, 0);
                            var tokenSymbol = GetStringArg(entry, 1);
                            var priceSymbol = GetStringArg(entry, 2);
                            var nftNumber = GetStringArg(entry, 3);

                            Token priceToken;
                            accountManager.GetTokenBySymbol(priceSymbol, PlatformKind.Phantasma, out priceToken);

                            var price = UnitConversion.ToDecimal(GetNumberArg(entry, 4), priceToken.decimals);

                            var untilDate = GetTimestampArg(entry, 5);

                            if (groupSize > 1)
                            {
                                if (sellTokenCounter == 0)
                                {
                                    // Desc line #1.
                                    sb.AppendLine("\u2605 Sell:");
                                    sb.AppendLine($"{tokenSymbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)},");
                                }
                                else if (sellTokenCounter == groupSize - 1)
                                {
                                    // Desc line #N.
                                    sb.AppendLine($"{tokenSymbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)}");
                                    sb.AppendLine($"for {price} {priceSymbol} each, offer valid until {untilDate}.");
                                }
                                else
                                {
                                    // Desc lines #2 ... N-1.
                                    sb.AppendLine($"{tokenSymbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)},");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"\u2605 Sell {tokenSymbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)} for {price} {priceSymbol}, offer valid until {untilDate}.");
                            }

                            sellTokenCounter++;

                            break;
                        }
                    case "Runtime.MintToken":
                        {
                            var owner = GetStringArg(entry, 0);
                            var recepient = GetStringArg(entry, 1);
                            var symbol = GetStringArg(entry, 2);
                            var bytes = GetByteArrayArg(entry, 3);

                            sb.AppendLine($"\u2605 Mint {symbol} NFT from {owner} with {recepient} as recipient.");
                            break;
                        }
                    case "Runtime.BurnTokens":
                        {
                            var address = GetStringArg(entry, 0);
                            var symbol = GetStringArg(entry, 1);
                            var amount = GetNumberArg(entry, 2);

                            Token token;
                            accountManager.GetTokenBySymbol(symbol, PlatformKind.Phantasma, out token);

                            var total = UnitConversion.ToDecimal(amount, token.decimals);

                            sb.AppendLine($"\u2605 Burn {total} {symbol} from {address}.");
                            break;
                        }
                    case "Runtime.InfuseToken":
                        {
                            var address = GetStringArg(entry, 0);
                            var targetSymbol = GetStringArg(entry, 1);
                            var tokenID = GetStringArg(entry, 2);
                            var infuseSymbol = GetStringArg(entry, 3);
                            var amount = GetNumberArg(entry, 4);

                            Token infuseToken;
                            accountManager.GetTokenBySymbol(infuseSymbol, PlatformKind.Phantasma, out infuseToken);

                            sb.AppendLine($"\u2605 Infuse {targetSymbol} NFT #{tokenID} with " + (infuseToken.flags.Contains("Fungible") ? $"{UnitConversion.ToDecimal(amount, infuseToken.decimals)} {infuseSymbol}." : $"{infuseSymbol} NFT #{amount}."));
                            break;
                        }
                    case "Nexus.CreateToken":
                        {
                            var address = GetStringArg(entry, 0);
                            var symbol = GetStringArg(entry, 1);
                            var name = GetStringArg(entry, 2);
                            var maxSupply = GetNumberArg(entry, 3);
                            var decimals = GetNumberArg(entry, 4);
                            var flags = GetNumberArg(entry, 5);
                            var ctScript = GetByteArrayArg(entry, 6);

                            sb.AppendLine($"\u2605 Create token {symbol} with name '{name}', {maxSupply} max supply, {decimals} decimals from {address}.");
                            break;
                        }

                    case "GHOST.mintToken":
                        {
                            var editionId = GetNumberArg(entry, 0);
                            var editionMax = GetNumberArg(entry, 1);
                            var editionMode = GetNumberArg(entry, 2);
                            var creator = GetStringArg(entry, 3);
                            var royalties = GetNumberArg(entry, 4);
                            var mintTicker = GetStringArg(entry, 5);
                            var numOfNfts = GetNumberArg(entry, 6);
                            var name = GetStringArg(entry, 7);
                            var description = GetStringArg(entry, 8);
                            var imageURL = GetStringArg(entry, 9);
                            var infoURL = GetStringArg(entry, 10);
                            var attributeType1 = GetStringArg(entry, 11);
                            var attributeValue1 = GetStringArg(entry, 12);
                            var attributeType2 = GetStringArg(entry, 13);
                            var attributeValue2 = GetStringArg(entry, 14);
                            var attributeType3 = GetStringArg(entry, 15);
                            var attributeValue3 = GetStringArg(entry, 16);
                            var lockedContent = GetStringArg(entry, 17);
                            var listPrice = GetNumberArg(entry, 18);
                            var listPriceCurrency = GetStringArg(entry, 19);
                            var listLastEndDate = GetTimestampArg(entry, 20);
                            var infusedAsset = GetStringArg(entry, 21);
                            var infusedAmount = GetNumberArg(entry, 22);

                            Token listPriceToken;
                            accountManager.GetTokenBySymbol(listPriceCurrency, PlatformKind.Phantasma, out listPriceToken);

                            Token infuseAmountToken;
                            accountManager.GetTokenBySymbol(infusedAsset, PlatformKind.Phantasma, out infuseAmountToken);

                            var price = UnitConversion.ToDecimal(GetNumberArg(entry, 4), priceToken.decimals);

                            sb.AppendLine($"\u2605 Mint {numOfNfts}x {mintTicker}, with a {royalty}% royalty.");
                            if (infusedAmount > 0)
                            {
                              sb.AppendLine($"\u2605 Infuse {numOfNfts}x {mintTicker} with {infuseAmountToken} {infusedAsset}.");
                            }
                            if (listPrice > 0)
                            {
                              sb.AppendLine($"\u2605 Sell {numOfNfts}x {mintTicker}, for {listPriceToken} {listPriceCurrency}, offer valid until {listLastEndDate}.");
                            }
                            break;
                        }

                    default:
                        sb.AppendLine(entry.ToString());
                        break;
                }

            }

            if (sb.Length > 0)
            {
                callback(sb.ToString(), null);
                yield break;
            }

            callback(null, "Unknown transaction content.");
        }
    }
}
