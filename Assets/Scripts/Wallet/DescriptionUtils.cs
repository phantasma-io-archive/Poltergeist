using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.SDK;
using Phantasma.VM.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
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

        private static Dictionary<string, int> methodTable = DisasmUtils.GetDefaultDisasmTable();


        public static void RegisterContractMethod(string contractMethod, int paramCount)
        {
            methodTable[contractMethod] = paramCount;
        }

        public static string GetDescription(byte[] script)
        {
            foreach (var entry in methodTable.Keys)
            {
                Debug.Log("disam method: " + entry);
            }

            var disasm = DisasmUtils.ExtractMethodCalls(script, methodTable);

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
                            AccountManager.Instance.GetTokenBySymbol(symbol, PlatformKind.Phantasma, out token);

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
                            AccountManager.Instance.GetTokenBySymbol(priceSymbol, PlatformKind.Phantasma, out priceToken);

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
                            AccountManager.Instance.GetTokenBySymbol(symbol, PlatformKind.Phantasma, out token);

                            var total = UnitConversion.ToDecimal(amount, token.decimals);

                            sb.AppendLine($"\u2605 Burn {total} {symbol} from {address}.");
                            break;
                        }

                    default:
                        sb.AppendLine(entry.ToString());
                        break;
                }

            }

            if (sb.Length > 0)
            {
                return sb.ToString();
            }

            return "Unknown transaction content.";
        }
    }
}
