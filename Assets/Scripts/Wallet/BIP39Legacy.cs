using System;
using System.Linq;

public static class BIP39Legacy
{
    public static string GenerateMnemonic()
    {
        throw new NotImplementedException();
    }

    public static byte[] MnemonicToPK(string mnemonicPhrase, string password)
    {
        var bip = new Bitcoin.BIP39.BIP39(mnemonicPhrase, password);
        return bip.SeedBytes.Take(32).ToArray();
    }
}
