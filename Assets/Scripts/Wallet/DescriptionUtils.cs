using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.SDK;
using Phantasma.VM.Utils;
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

        public static string GetDescription(byte[] script)
        {
            var table = DisasmUtils.GetDefaultDisasmTable();
            // adding missing stuff here like table["methodname"] = 5 where 5 is arg count

            var disasm = DisasmUtils.ExtractMethodCalls(script, table);

            // Checking if all calls are "market.SellToken" calls and we can group them.
            int sellGroupSize = 0;
            DisasmMethodCall? prevCall = null;
            foreach (var call in disasm)
            {
                if (CheckIfCallShouldBeIgnored(call))
                {
                    continue;
                }

                if (GetCallFullName(call) == "market.SellToken")
                {
                    if (prevCall != null && !CompareCalls((DisasmMethodCall)prevCall, call, 0, 2, 4, 5))
                    {
                        sellGroupSize = 0;
                        break;
                    }

                    prevCall = call;
                    sellGroupSize++;
                }
                else
                {
                    // Different call, grouping is not supported.
                    sellGroupSize = 0;
                    break;
                }
            }

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
                    case "Runtime.TransferTokens":
                        {
                            var src = entry.Arguments[0].AsString();
                            var dst = entry.Arguments[1].AsString();
                            var symbol = entry.Arguments[2].AsString();
                            var amount = entry.Arguments[3].AsNumber();

                            Token token;
                            AccountManager.Instance.GetTokenBySymbol(symbol, out token);

                            var total = UnitConversion.ToDecimal(amount, token.decimals);

                            sb.AppendLine($"Transfer {total} {symbol} from {src} to {dst}.");
                            break;
                        }
                    case "market.BuyToken":
                        {
                            var dst = entry.Arguments[0].AsString();
                            var symbol = entry.Arguments[1].AsString();
                            var nftNumber = entry.Arguments[2].AsString();

                            sb.AppendLine($"Buy {symbol} NFT #{nftNumber.Substring(0 ,5) + "..." + nftNumber.Substring(nftNumber.Length - 5)}.");
                            break;
                        }
                    case "market.SellToken":
                        {
                            var dst = entry.Arguments[0].AsString();
                            var tokenSymbol = entry.Arguments[1].AsString();
                            var priceSymbol = entry.Arguments[2].AsString();
                            var nftNumber = entry.Arguments[3].AsString();

                            Token priceToken;
                            AccountManager.Instance.GetTokenBySymbol(priceSymbol, out priceToken);

                            var price = UnitConversion.ToDecimal(entry.Arguments[4].AsNumber(), priceToken.decimals);

                            var untilDate = entry.Arguments[5].AsTimestamp();

                            if (sellGroupSize > 1)
                            {
                                if (sellTokenCounter == 0)
                                {
                                    // Desc line #1.
                                    sb.AppendLine("Sell:");
                                    sb.AppendLine($"{tokenSymbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)},");
                                }
                                else if (sellTokenCounter == sellGroupSize - 1)
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
                                sb.AppendLine($"Sell {tokenSymbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)} for {price} {priceSymbol}, offer valid until {untilDate}.");
                            }

                            sellTokenCounter++;

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
