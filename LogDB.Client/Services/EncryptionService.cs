using System.Security.Cryptography;
using System.Text;

namespace LogDB.Client.Services;

/// <summary>
/// Client-side field encryption service
/// </summary>
public static class EncryptionService
{
    private static byte[]? _key;
    private static byte[]? _iv;

    private static void EnsureInitialized()
    {
        if (_key != null && _iv != null)
            return;

        string? envKey = Environment.GetEnvironmentVariable("LOGDB_SECRET_KEY");

        if (string.IsNullOrWhiteSpace(envKey))
        {
            throw new InvalidOperationException(
                "LOGDB_SECRET_KEY environment variable is required for encryption. " +
                "Set this variable to a secure, random string (at least 32 characters) " +
                "before using field encryption features.");
        }

        using (SHA256 sha256 = SHA256.Create())
        {
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(envKey));
        }

        _iv = new byte[16];
    }

    public static string Encrypt(string plainText)
    {
        EnsureInitialized();

        using Aes aesAlg = Aes.Create();
        aesAlg.Key = _key!;
        aesAlg.IV = _iv!;
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using MemoryStream msEncrypt = new MemoryStream();
        using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return "encrypted_by_logdb:" + Convert.ToBase64String(msEncrypt.ToArray());
    }

    public static string Decrypt(string cipherText)
    {
        if (cipherText.StartsWith("encrypted_by_logdb:"))
        {
            cipherText = cipherText.Replace("encrypted_by_logdb:", "");
        }
        else
        {
            return cipherText;
        }

        EnsureInitialized();

        using Aes aesAlg = Aes.Create();
        aesAlg.Key = _key!;
        aesAlg.IV = _iv!;
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
        using MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
        using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using StreamReader srDecrypt = new StreamReader(csDecrypt);
        return srDecrypt.ReadToEnd();
    }

    public static string Encrypt(long number)
    {
        EnsureInitialized();

        byte[] inputBytes = BitConverter.GetBytes(number);

        using Aes aes = Aes.Create();
        aes.Key = _key!;
        aes.IV = _iv!;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream ms = new MemoryStream();
        using CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(inputBytes, 0, inputBytes.Length);
        cs.FlushFinalBlock();

        return Convert.ToBase64String(ms.ToArray());
    }

    public static long DecryptNumber(string encryptedBase64)
    {
        EnsureInitialized();

        byte[] cipherBytes = Convert.FromBase64String(encryptedBase64);

        using Aes aes = Aes.Create();
        aes.Key = _key!;
        aes.IV = _iv!;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream ms = new MemoryStream(cipherBytes);
        using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);

        byte[] decryptedBytes = new byte[8];
#if NETFRAMEWORK
        int totalRead = 0;
        while (totalRead < decryptedBytes.Length)
        {
            int bytesRead = cs.Read(decryptedBytes, totalRead, decryptedBytes.Length - totalRead);
            if (bytesRead == 0) throw new EndOfStreamException();
            totalRead += bytesRead;
        }
#else
        cs.ReadExactly(decryptedBytes);
#endif

        return BitConverter.ToInt64(decryptedBytes, 0);
    }
}
