using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Neo2;
using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using NBitcoin.DataEncoders;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Phantasma.Core.Cryptography.ECDsa;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Phantasma.Core.Utils;
using Poltergeist.Neo2.Utils;
using UnityEngine;
using ECCurve = System.Security.Cryptography.ECCurve;
using ECPoint = System.Security.Cryptography.ECPoint;

namespace Poltergeist.Neo2.Core
{
    public class NeoKeys 
    {
        public readonly byte[] PrivateKey;
        public readonly byte[] PublicKey;
        public readonly byte[] CompressedPublicKey;
        public readonly UInt160 PublicKeyHash;
        public readonly string Address;
        public readonly string AddressN3;
        public readonly string WIF;

        public readonly UInt160 signatureHashN2;
        public readonly UInt160 signatureHashN3;
        public readonly byte[] signatureScriptN2;
        public readonly byte[] signatureScriptN3;

        public NeoKeys(byte[] privateKey)
        {
            if (privateKey.Length != 32 && privateKey.Length != 96 && privateKey.Length != 104)
                throw new ArgumentException();
            this.PrivateKey = new byte[32];
            privateKey= privateKey[^32..];
            Buffer.BlockCopy(privateKey, privateKey.Length - 32, PrivateKey, 0, 32);
            this.PrivateKey = privateKey[^32..];

            this.CompressedPublicKey = ECDsa.GetPublicKey(this.PrivateKey, true, ECDsaCurve.Secp256r1);
            
            this.PublicKeyHash = NeoUtils.ToScriptHash(this.CompressedPublicKey);

            this.signatureScriptN2 = CreateSignatureScript(this.CompressedPublicKey);
            signatureHashN2 = NeoUtils.ToScriptHash(signatureScriptN2);
            
            this.signatureScriptN3 = CreateSignatureScriptN3(this.CompressedPublicKey);
            signatureHashN3 = NeoUtils.ToScriptHash(signatureScriptN3);
            
            this.PublicKey = ECDsa.GetPublicKey(this.PrivateKey, false, ECDsaCurve.Secp256r1).Skip(1).ToArray();
            
            this.Address = NeoUtils.ToAddress(signatureHashN2);
            this.AddressN3 = NeoUtils.ToAddressN3(signatureHashN3);
            this.WIF = GetWIF();
        }

        public static NeoKeys FromWIF(string wif)
        {
            if (wif == null) throw new ArgumentNullException();
            byte[] data = wif.Base58CheckDecode();
            if (data.Length != 34 || data[0] != 0x80 || data[33] != 0x01)
                throw new FormatException();
            byte[] privateKey = new byte[32];
            Buffer.BlockCopy(data, 1, privateKey, 0, privateKey.Length);
            Array.Clear(data, 0, data.Length);
            return new NeoKeys(privateKey);
        }

        public static NeoKeys FromNEP2(string nep2, string passphrase, int N = 16384, int r = 8, int p = 8)
        {
            if (nep2 == null)
            {
                throw new ArgumentNullException(nameof(nep2));
            }
            if (passphrase == null)
            {
                throw new ArgumentNullException(nameof(passphrase));
            }

            byte[] data = nep2.Base58CheckDecode();
            if (data.Length != 39 || data[0] != 0x01 || data[1] != 0x42 || data[2] != 0xe0)
                throw new FormatException();

            byte[] addressHash = new byte[4];
            Buffer.BlockCopy(data, 3, addressHash, 0, 4);
            byte[] datapassphrase = Encoding.UTF8.GetBytes(passphrase);
            byte[] derivedkey = SCrypt.Generate(datapassphrase, addressHash, N, r, p, 64);
            Array.Clear(datapassphrase, 0, datapassphrase.Length);

            byte[] derivedhalf1 = derivedkey.Take(32).ToArray();
            byte[] derivedhalf2 = derivedkey.Skip(32).ToArray();
            Array.Clear(derivedkey, 0, derivedkey.Length);

            byte[] encryptedkey = new byte[32];
            Buffer.BlockCopy(data, 7, encryptedkey, 0, 32);
            Array.Clear(data, 0, data.Length);

            byte[] prikey = XOR(encryptedkey.AES256Decrypt(derivedhalf2), derivedhalf1);
            Array.Clear(derivedhalf1, 0, derivedhalf1.Length);
            Array.Clear(derivedhalf2, 0, derivedhalf2.Length);

            var keys = new NeoKeys(prikey);
            var temp = Encoding.ASCII.GetBytes(keys.Address).Sha256().Sha256().Take(4).ToArray();
            if (!temp.SequenceEqual(addressHash))
            {
                throw new FormatException("invalid passphrase when decrypting NEP2");
            }
            return keys;
        }

        public static byte[] CreateSignatureScript(byte[] bytes)
        {
            var script = new byte[bytes.Length + 2];

            script[0] = (byte) OpCode.PUSHBYTES33;
            Array.Copy(bytes, 0, script, 1, bytes.Length);
            script[script.Length - 1] = (byte) OpCode.CHECKSIG;

            return  script;
        }
        
        public static byte[] CreateSignatureScriptN3(byte[] bytes)
        {
            var sb = new ScriptBuilder();
            sb.EmitPush(EncodePoint(bytes));
            sb.Emit(OpCode.SYSCALL, BitConverter.GetBytes(666101590));
            var endScript = sb.ToArray();

            return endScript;
        }

        public static byte[] EncodePoint(byte[] bytes)
        {
            byte[] data = new byte[33];
            Array.Copy(bytes, 0, data, 33 - bytes.Length, bytes.Length);
            data[0] = (byte)0x03;
            return data;
        }
      
        private string GetWIF()
        {
            byte[] data = new byte[34];
            data[0] = 0x80;
            Buffer.BlockCopy(PrivateKey, 0, data, 1, 32);
            data[33] = 0x01;
            string wif = data.Base58CheckEncode();
            Array.Clear(data, 0, data.Length);
            return wif;
        }

        private static byte[] XOR(byte[] x, byte[] y)
        {
            if (x.Length != y.Length) throw new ArgumentException();
            return x.Zip(y, (a, b) => (byte)(a ^ b)).ToArray();
        }

        public override string ToString()
        {
            return this.Address;
        }
    }
}
