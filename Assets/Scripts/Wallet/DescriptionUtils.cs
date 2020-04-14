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

                switch (entry.MethodName)
                {
                    case "Runtime.TransferTokens":
                        {
                            var src = Address.FromBytes(entry.Arguments[0].AsByteArray());
                            var dst = Address.FromBytes(entry.Arguments[1].AsByteArray());
                            var symbol = entry.Arguments[2].AsString();
                            var amount = entry.Arguments[3].AsNumber();

                            Token token;
                            AccountManager.Instance.GetTokenBySymbol(symbol, out token);

                            var total = UnitConversion.ToDecimal(amount, token.decimals);

                            sb.AppendLine($"Transfer {total} {symbol} from {src.Text} to {dst.Text}.");
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
