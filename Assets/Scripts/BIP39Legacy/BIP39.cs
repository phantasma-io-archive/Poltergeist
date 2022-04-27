using System;
using System.Text;

namespace Bitcoin.BIP39
{
    /// <summary>
    /// A .NET implementation of the Bitcoin Improvement Proposal - 39 (BIP39)
    /// BIP39 specification used as reference located here: https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki
    /// Made by thashiznets@yahoo.com.au
    /// v1.0.1.1
    /// I ♥ Bitcoin :)
    /// Bitcoin:1ETQjMkR1NNh4jwLuN5LxY7bMsHC9PUPSV
    /// </summary>
    public class BIP39
    {
        #region Private Attributes

        private byte[] _passphraseBytes;
        private string _mnemonicSentence;

        #endregion

        #region Public Constants and Enums

        public const string cEmptyString = "";
        public const string cSaltHeader = "mnemonic"; //this is the first part of the salt as described in the BIP39 spec

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor to build a BIP39 object using a supplied Mnemonic sentence and passphrase. If you are not worried about saving the entropy bytes, or using custom words not in a wordlist, you should consider the static method to do this instead.
        /// </summary>
        /// <param name="mnemonicSentence">The mnemonic sentencs used to derive seed bytes, Please ensure NFKD Normalized</param>
        /// <param name="passphrase">Optional passphrase used to protect seed bytes, defaults to empty</param>
        /// <param name="language">Optional language to use for wordlist, if not specified it will auto detect language and if it can't detect it will default to English</param>
        public BIP39(string mnemonicSentence, string passphrase = cEmptyString)
        {
            _mnemonicSentence = Utilities.NormaliseStringNfkd(mnemonicSentence.Trim()); //just making sure we don't have any leading or trailing spaces
            _passphraseBytes = UTF8Encoding.UTF8.GetBytes(Utilities.NormaliseStringNfkd(passphrase));
            string[] words = _mnemonicSentence.Split(new char[] { ' ' });

            //if the sentence is not at least 12 characters or cleanly divisible by 3, it is bad!
            if (words.Length < 12 || words.Length % 3 != 0)
            {
                throw new Exception("Mnemonic sentence must be at least 12 words and it will increase by 3 words for each increment in entropy. Please ensure your sentence is at leas 12 words and has no remainder when word count is divided by 3");
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the mnemonic sentence built from ent+cs
        /// </summary>
        public string MnemonicSentence
        {
            get
            {
                return _mnemonicSentence;
            }
        }

        /// <summary>
        /// Gets the bytes of the seed created from the mnemonic sentence. This could become your root in BIP32
        /// </summary>
        public byte[] SeedBytes
        {
            get
            {
                //literally this is the bulk of the decoupled seed generation code, easy.
                byte[] salt = Utilities.MergeByteArrays(UTF8Encoding.UTF8.GetBytes(cSaltHeader),_passphraseBytes);
                return Rfc2898_pbkdf2_hmacsha512.PBKDF2(UTF8Encoding.UTF8.GetBytes(Utilities.NormaliseStringNfkd(MnemonicSentence)), salt);
            }
        }
        #endregion
    }
}
