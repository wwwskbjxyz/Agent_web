using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SProtectPlatform.Api.Options;

namespace SProtectPlatform.Api.Services;

public interface ICredentialProtector
{
    string Protect(string plainText);
    string Unprotect(string cipherText);
}

public sealed class CredentialProtector : ICredentialProtector
{
    private readonly byte[] _key;

    public CredentialProtector(IOptions<EncryptionOptions> options)
    {
        var raw = options.Value.CredentialKey ?? string.Empty;
        if (raw.Length < 32)
        {
            throw new InvalidOperationException("CredentialKey must be at least 32 characters long.");
        }

        _key = Encoding.UTF8.GetBytes(raw[..32]);
    }

    public string Protect(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var inputBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return Convert.ToBase64String(result);
    }

    public string Unprotect(string cipherText)
    {
        var raw = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var blockSize = aes.BlockSize / 8;
        var iv = new byte[blockSize];
        Buffer.BlockCopy(raw, 0, iv, 0, blockSize);
        var cipher = new byte[raw.Length - blockSize];
        Buffer.BlockCopy(raw, blockSize, cipher, 0, cipher.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
}
