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
        public static string GetDescription(byte[] script)
        {
            var table = DisasmUtils.GetDefaultDisasmTable();
            // adding missing stuff here like table["methodname"] = 5 where 5 is arg count

            var disasm = DisasmUtils.ExtractMethodCalls(script, table);

            var sb = new StringBuilder();
            foreach (var entry in disasm)
            {
                if ("gas".Equals(entry.ContractName) && (entry.MethodName == "AllowGas" || entry.MethodName == "SpendGas"))
                {
                    continue;
                }

                string name;
                if (!string.IsNullOrEmpty(entry.ContractName))
                    name = $"{entry.ContractName}.{entry.MethodName}";
                else
                    name = entry.MethodName;

                // Put it to log so that developer can easily check what PG is receiving.
                Log.Write("GetDescription(): Contract's description: " + entry.ToString());

                switch (name)
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

                            sb.AppendLine($"Sell {tokenSymbol} NFT #{nftNumber.Substring(0, 5) + "..." + nftNumber.Substring(nftNumber.Length - 5)} for {price} {priceSymbol}, offer valid until {untilDate}.");
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
