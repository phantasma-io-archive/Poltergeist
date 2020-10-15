using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;

/// <summary>
/// Contains the relevant Bouncy Castle Methods required to encrypt a password.
/// References NuGet Package BouncyCastle.Crypto.dll
/// </summary>
public class BouncyCastleHashing
{
    private SecureRandom _cryptoRandom;

    public BouncyCastleHashing()
    {
        _cryptoRandom = new SecureRandom();
    }

    /// <summary>
    /// Random Salt Creation
    /// </summary>
    /// <param name="size">The size of the salt in bytes</param>
    /// <returns>A random salt of the required size.</returns>
    public byte[] CreateSalt(int size)
    {
        byte[] salt = new byte[size];
        _cryptoRandom.NextBytes(salt);
        return salt;
    }

    /// <summary>
    /// Gets a PBKDF2_SHA256 Hash  (Overload)
    /// </summary>
    /// <param name="password">The password as a plain text string</param>
    /// <param name="saltAsBase64String">The salt for the password</param>
    /// <param name="iterations">The number of times to encrypt the password</param>
    /// <param name="hashByteSize">The byte size of the final hash</param>
    /// <returns>A base64 string of the hash.</returns>
    public string PBKDF2_SHA256_GetHash(string password, string saltAsBase64String, int iterations, int hashByteSize)
    {
        var saltBytes = Convert.FromBase64String(saltAsBase64String);

        var hash = PBKDF2_SHA256_GetHash(password, saltBytes, iterations, hashByteSize);

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Gets a PBKDF2_SHA256 Hash (CORE METHOD)
    /// </summary>
    /// <param name="password">The password as a plain text string</param>
    /// <param name="salt">The salt as a byte array</param>
    /// <param name="iterations">The number of times to encrypt the password</param>
    /// <param name="hashByteSize">The byte size of the final hash</param>
    /// <returns>A the hash as a byte array.</returns>
    public byte[] PBKDF2_SHA256_GetHash(string password, byte[] salt, int iterations, int hashByteSize)
    {
        var pdb = new Pkcs5S2ParametersGenerator(new Org.BouncyCastle.Crypto.Digests.Sha256Digest());
        pdb.Init(PbeParametersGenerator.Pkcs5PasswordToBytes(password.ToCharArray()), salt,
                     iterations);
        var key = (KeyParameter)pdb.GenerateDerivedMacParameters(hashByteSize * 8);
        return key.GetKey();
    }
}
