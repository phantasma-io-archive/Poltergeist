using System;
using System.IO;
using JetBrains.Annotations;
using Phantasma.Core.Domain;
using Phantasma.Core.Utils;

namespace Poltergeist
{
    public struct Account : ISerializable
    {
        public string name;
        public PlatformKind platforms;
        public string phaAddress;
        public string neoAddress;
        public string ethAddress;
        public string WIF;
        public bool passwordProtected;
        public int passwordIterations;
        public string salt;
        public string iv;
        public string password; // Not used after account upgrade to version 2.
        public string misc;
        public string neoAddressN3;
        public int version; 
        
        public override string ToString()
        {
            return $"{name.ToUpper()} [{platforms}]";
        }

        public string GetWif(string passwordHash)
        {
            return String.IsNullOrEmpty(passwordHash) ? WIF : AccountManager.DecryptString(WIF, passwordHash, iv);
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(name);
            uint platformsVal = (uint)Convert.ChangeType(platforms, typeof(uint));
            writer.WriteVarInt(platformsVal);
            writer.WriteVarString(phaAddress);
            writer.WriteVarString(neoAddress);
            writer.WriteVarString(ethAddress);
            writer.WriteVarString(WIF);
            writer.Write((byte)(((bool)passwordProtected) ? 1 : 0));
            writer.Write((int)passwordIterations);
            writer.WriteVarString(salt);
            writer.WriteVarString(iv);
            writer.WriteVarString(password);
            writer.WriteVarString(misc);
            if (version == 3)
            {
                writer.WriteVarString(!string.IsNullOrEmpty(neoAddressN3) ? neoAddressN3 : "");
                writer.Write((int)version);
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            name = reader.ReadVarString();
            var platformsVal = (uint)reader.ReadVarInt();
            platforms = PlatformKind.Parse<PlatformKind>(platformsVal.ToString());
            phaAddress = reader.ReadVarString();
            neoAddress = reader.ReadVarString();
            ethAddress = reader.ReadVarString();
            WIF = reader.ReadVarString();
            passwordProtected = reader.ReadByte() != 0;
            passwordIterations = reader.ReadInt32();
            salt = reader.ReadVarString();
            iv = reader.ReadVarString();
            password = reader.ReadVarString();
            misc = reader.ReadVarString();
            var headerReader = reader.BaseStream.Position;
            try
            {
                neoAddressN3 = reader.ReadVarString();
                version = reader.ReadInt32() != 3 ? 2 : 3;
                
                if (version == 2)
                {
                    reader.BaseStream.Position = headerReader;
                    neoAddressN3 = "";
                    version = 2;
                    return;
                }
                
                if (string.IsNullOrEmpty(neoAddressN3) || !neoAddressN3.StartsWith("N") || neoAddressN3.Length < 21 )
                {
                    return;
                }
            }
            catch (Exception)
            {
                reader.BaseStream.Position = headerReader;
                neoAddressN3 = null;
                version = 2;
            }
        }
    }
}
