using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Ethereum;
using Phantasma.Cryptography.ECC;
using Phantasma.Storage;
using System.Collections;

namespace Phantasma.Tests
{
    public class CryptoTests
    {
        [UnityTest]
        public IEnumerator ECDsaSecP256k1()
        {
            // Eth address: "0x66571c32d77c4852be4c282eb952ba94efbeac20";
            var key = "6f6784731c4e526c97fa6a97b6f22e96f307588c5868bc2c545248bc31207eb1";
            Assert.IsTrue(key.Length == 64);

            var privBytes = Base16.Decode(key);
            var phantasmaKeys = new PhantasmaKeys(privBytes);

            var wif = phantasmaKeys.ToWIF();
            var ethKeys = EthereumKey.FromWIF(wif);
            Debug.Log("Eth address: " + ethKeys);

            var pKey = ECCurve.Secp256k1.G * privBytes;
            var ethPublicKeyCompressed = pKey.EncodePoint(true).ToArray();
            Debug.Log("Eth compressed public key: " + Base16.Encode(ethPublicKeyCompressed));
            var ethPublicKeyUncompressed = pKey.EncodePoint(false).Skip(1).ToArray();
            Debug.Log("Eth uncompressed public key: " + Base16.Encode(ethPublicKeyUncompressed));

            var msgBytes = Encoding.ASCII.GetBytes("Phantasma");
            var signature = ethKeys.Sign(msgBytes, (message, prikey, pubkey) =>
            {
                return Poltergeist.Neo2.Utils.CryptoUtils.Sign(message, prikey, pubkey, Poltergeist.Neo2.Cryptography.ECC.ECDsaCurve.Secp256k1);
            });

            var ecdsaSignature = (ECDsaSignature)signature;
            var signatureSerialized = signature.Serialize(); // signature.ToByteArray() gives same result

            Debug.Log("\nSignature (RAW concatenated r & s, hex):\n" + Base16.Encode(ecdsaSignature.Bytes));
            // Curve byte: ECDsaCurve enum: Secp256r1 = 0, Secp256k1 = 1.
            // Following is the format we use for signature:
            Debug.Log("\nSignature (curve byte + signature length + concatenated r & s, hex):\n" + Base16.Encode(signatureSerialized));

            var signatureDEREncoded = ethKeys.Sign(msgBytes, (message, prikey, pubkey) =>
            {
                return Poltergeist.Neo2.Utils.CryptoUtils.Sign(message, prikey, pubkey, Poltergeist.Neo2.Cryptography.ECC.ECDsaCurve.Secp256k1, Poltergeist.Neo2.Utils.CryptoUtils.SignatureFormat.DEREncoded);
            });

            var ecdsaSignatureDEREncoded = (ECDsaSignature)signatureDEREncoded;

            Debug.Log("\nSignature (RAW DER-encoded, hex):\n" + Base16.Encode(ecdsaSignatureDEREncoded.Bytes));
            Debug.Log("\nSignature (curve byte + signature length + DER-encoded, hex):\n" + Base16.Encode(signatureDEREncoded.Serialize()));

            // Since ECDsaSignature class not working for us,
            // we use signature .Bytes directly to verify it with Bouncy Castle.
            // Verifying concatenated signature / compressed Eth public key.
            Assert.IsTrue(Poltergeist.Neo2.Utils.CryptoUtils.Verify(msgBytes, ecdsaSignature.Bytes, ethPublicKeyCompressed, Poltergeist.Neo2.Cryptography.ECC.ECDsaCurve.Secp256k1));

            // Verifying concatenated signature / uncompressed Eth public key.
            // Not working with Bouncy Castle.
            // Assert.IsTrue(Phantasma.Neo.Utils.CryptoUtils.Verify(msgBytes, ecdsaSignature.Bytes, ethPublicKeyUncompressed, ECDsaCurve.Secp256k1));

            // Verifying DER signature.
            Assert.IsTrue(Poltergeist.Neo2.Utils.CryptoUtils.Verify(msgBytes, ecdsaSignatureDEREncoded.Bytes, ethPublicKeyCompressed, Poltergeist.Neo2.Cryptography.ECC.ECDsaCurve.Secp256k1, Poltergeist.Neo2.Utils.CryptoUtils.SignatureFormat.DEREncoded));

            // This method we cannot use, it gives "System.NotImplementedException : The method or operation is not implemented."
            // exception in Unity, because Unity does not fully support .NET cryptography.
            // Assert.IsTrue(((ECDsaSignature)signature).Verify(msgBytes, Address.FromKey(ethKeys)));

            // Failes for same reason: "System.NotImplementedException".
            // Assert.IsTrue(CryptoExtensions.VerifySignatureECDsa(msgBytes, signatureSerialized, ethPublicKeyCompressed, ECDsaCurve.Secp256k1));

            yield return null;
        }
    }

}
