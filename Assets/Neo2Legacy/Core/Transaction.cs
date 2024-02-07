using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Neo2;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Poltergeist.Neo2.Core
{
    public enum TransactionAttributeUsage
    {
        ContractHash = 0x00,
        ECDH02 = 0x02,
        ECDH03 = 0x03,
        Script = 0x20,
        Vote = 0x30,
        DescriptionUrl = 0x81,
        Description = 0x90,

        Hash1 = 0xa1,
        Hash2 = 0xa2,
        Hash3 = 0xa3,
        Hash4 = 0xa4,
        Hash5 = 0xa5,
        Hash6 = 0xa6,
        Hash7 = 0xa7,
        Hash8 = 0xa8,
        Hash9 = 0xa9,
        Hash10 = 0xaa,
        Hash11 = 0xab,
        Hash12 = 0xac,
        Hash13 = 0xad,
        Hash14 = 0xae,
        Hash15 = 0xaf,

        Remark = 0xf0,
        Remark1 = 0xf1,
        Remark2 = 0xf2,
        Remark3 = 0xf3,
        Remark4 = 0xf4,
        Remark5 = 0xf5,
        Remark6 = 0xf6,
        Remark7 = 0xf7,
        Remark8 = 0xf8,
        Remark9 = 0xf9,
        Remark10 = 0xfa,
        Remark11 = 0xfb,
        Remark12 = 0xfc,
        Remark13 = 0xfd,
        Remark14 = 0xfe,
        Remark15 = 0xff
    }

    public struct TransactionAttribute
    {
        public TransactionAttributeUsage Usage;
        public byte[] Data;

        public TransactionAttribute(TransactionAttributeUsage usage, byte[] data)
        {
            Usage = usage;
            Data = data;
        }

        internal void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Usage);
            if (Usage == TransactionAttributeUsage.DescriptionUrl)
                writer.Write((byte)Data.Length);
            else if (Usage == TransactionAttributeUsage.Description || Usage >= TransactionAttributeUsage.Remark)
                writer.WriteVarInt(Data.Length);
            if (Usage == TransactionAttributeUsage.ECDH02 || Usage == TransactionAttributeUsage.ECDH03)
                writer.Write(Data, 1, 32);
            else
                writer.Write(Data);
        }
    }

    public class Witness
    {
        public byte[] invocationScript;
        public byte[] verificationScript;

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(this.invocationScript);
            writer.WriteVarBytes(this.verificationScript);
        }
    }

    public enum TransactionType : byte
    {
        ClaimTransaction = 0x02,
        ContractTransaction = 0x80,
        InvocationTransaction = 0xd1
    }

    public class Transaction 
    {
        public class Input 
        {
            public UInt256 prevHash;
            public uint prevIndex;
        }

        public class Output
        {
            public UInt160 scriptHash;
            public byte[] assetID;
            public decimal value;
        }

        public TransactionType type;
        public byte version;
        public byte[] script;
        public decimal gas;

        public Input[] inputs;
        public Output[] outputs;
        public Witness[] witnesses;

        public Input[] claimReferences;
        public TransactionAttribute[] attributes;

        public string interop;

        public uint nonce;

        #region HELPERS
        protected static void SerializeTransactionInput(BinaryWriter writer, Input input)
        {
            writer.Write(input.prevHash.ToArray());
            writer.Write((ushort)input.prevIndex);
        }

        protected static void SerializeTransactionOutput(BinaryWriter writer, Output output)
        {
            writer.Write(output.assetID);
            writer.WriteFixed(output.value);
            writer.Write(output.scriptHash.ToArray());
        }
        #endregion

        public override string ToString()
        {
            return Hash.ToString();
        }

        public byte[] Serialize(bool signed = true)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)this.type);
                    writer.Write((byte)this.version);

                    // exclusive data
                    switch (this.type)
                    {
                        case TransactionType.InvocationTransaction:
                            {
                                writer.WriteVarInt(this.script.Length);
                                writer.Write(this.script);
                                if (this.version >= 1)
                                {
                                    writer.WriteFixed(this.gas);
                                }
                                break;
                            }

                        case TransactionType.ClaimTransaction:
                            {
                                writer.WriteVarInt(this.claimReferences.Length);
                                foreach (var entry in this.claimReferences)
                                {
                                    SerializeTransactionInput(writer, entry);
                                }
                                break;
                            }
                    }

                    // Don't need any attributes
                    if (this.attributes != null)
                    {
                        writer.WriteVarInt(this.attributes.Length);
                        foreach (var attr in this.attributes)
                        {
                            attr.Serialize(writer);
                        }
                    }
                    else
                    {
                        writer.Write((byte)0);
                    }

                    if (this.inputs != null)
                    {
                        writer.WriteVarInt(this.inputs.Length);
                        foreach (var input in this.inputs)
                        {
                            SerializeTransactionInput(writer, input);
                        }
                    }
                    else
                    {
                        writer.Write((byte)0);
                    }

                    if (this.outputs != null)
                    {
                        writer.WriteVarInt(this.outputs.Length);
                        foreach (var output in this.outputs)
                        {
                            SerializeTransactionOutput(writer, output);
                        }
                    }
                    else
                    {
                        writer.Write((byte)0);
                    }

                    if (signed)
                    {
                        if (this.witnesses != null)
                        {
                            writer.WriteVarInt(this.witnesses.Length);
                            foreach (var witness in this.witnesses)
                            {
                                witness.Serialize(writer);
                            }
                        }
                        else
                        {
                            writer.Write((byte)0);
                        }
                    }

                }

                return stream.ToArray();
            }
        }

        public void Sign(NeoKeys key, IEnumerable<Witness> witnesses = null)
        {
            // append Phantasma address to end of verification script
            if (interop != null)
            {
                var interopBytes = Encoding.UTF8.GetBytes(interop);

                var temp = this.attributes != null ? this.attributes.ToList() : new List<TransactionAttribute>();
                temp.Add(new TransactionAttribute(TransactionAttributeUsage.Description, interopBytes));
                this.attributes = temp.ToArray();
            }

            var txdata = this.Serialize(false);

            var witList = new List<Witness>();

            if (key != null)
            {
                var privkey = key.PrivateKey;
                var pubkey = key.PublicKey;
                var signature = CryptoUtils.Sign(txdata, privkey, pubkey);

                var invocationScript = new byte[] { (byte)OpCode.PUSHBYTES64 }.Concat(signature).ToArray();
                var verificationScript = new byte[key.signatureScriptN2.Length];

                for (int i=0; i<verificationScript.Length; i++)
                {
                    verificationScript[i] = key.signatureScriptN2[i];
                }

                witList.Add(new Witness() { invocationScript = invocationScript, verificationScript = verificationScript });
            }

            if (witnesses != null)
            {
                foreach (var entry in witnesses)
                {
                    witList.Add(entry);
                }
            }

            this.witnesses = witList.ToArray();
        }

        private UInt256 _hash = null;

        public UInt256 Hash
        {
            get
            {
                if (_hash == null)
                {
                    var rawTx = this.Serialize(false);
                    var hex = rawTx.ByteToHex();
                    _hash = new UInt256(NeoUtils.Hash256(rawTx));
                }

                return _hash;
            }
        }
    }

}
