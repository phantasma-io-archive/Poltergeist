using Org.BouncyCastle.Asn1.Sec;
using NBitcoin;
using System;

public static class BIP39NBitcoin
{
    public static string GenerateMnemonic(Poltergeist.MnemonicPhraseLength mnemonicPhraseLength)
    {
        Mnemonic mnemo = new Mnemonic(Wordlist.English, mnemonicPhraseLength == Poltergeist.MnemonicPhraseLength.Twelve_Words ? WordCount.Twelve : WordCount.TwentyFour);
        return mnemo.ToString();
    }

    public static byte[] MnemonicToPK(string mnemonicPhrase, uint pkIndex = 0)
    {
        var mnemonic = new Mnemonic(mnemonicPhrase);
        var keyPathToDerive = KeyPath.Parse("m/44'/60'/0'/0");
        var pk = new ExtKey(mnemonic.DeriveSeed(null)).Derive(keyPathToDerive);
        var keyNew = pk.Derive(pkIndex);
        var pkeyBytes = keyNew.PrivateKey.PubKey.ToBytes();
        var ecParams = SecNamedCurves.GetByName("secp256k1");
        var point = ecParams.Curve.DecodePoint(pkeyBytes);
        var xCoord = point.XCoord.GetEncoded();
        var yCoord = point.YCoord.GetEncoded();
        var uncompressedBytes = new byte[64];
        // copy X coordinate
        Array.Copy(xCoord, uncompressedBytes, xCoord.Length);
        // copy Y coordinate
        for (int i = 0; i < 32 && i < yCoord.Length; i++)
        {
            uncompressedBytes[uncompressedBytes.Length - 1 - i] = yCoord[yCoord.Length - 1 - i];
        }
        return keyNew.PrivateKey.ToBytes();
    }
    public static string MnemonicToWif(string mnemonicPhrase, uint pkIndex = 0)
    {
        var privKey = BIP39NBitcoin.MnemonicToPK(mnemonicPhrase, pkIndex);
        var phaKeys = new Phantasma.Cryptography.PhantasmaKeys(privKey);
        return phaKeys.ToWIF();
    }
}
