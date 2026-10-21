using System.Security.Cryptography;
using System.Text;
using BS2.Application;
using BS2.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace BS2.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM with a random 96-bit nonce per value. Stored format: <c>enc:v1:</c> + base64(nonce ‖ tag ‖ ciphertext).
/// Values without the prefix are returned as-is, so a column can be encrypted in place by a later
/// backfill without breaking reads. <see cref="Hash"/> is HMAC-SHA256 under a key derived from the
/// master key, so hashes are useless without it.
/// </summary>
public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;
    private readonly byte[] _hmacKey;

    public AesGcmFieldEncryptor(IOptions<EncryptionOptions> options) : this(options.Value.MasterKey)
    {
    }

    public AesGcmFieldEncryptor(string masterKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(masterKeyBase64))
            throw new InvalidOperationException("Encryption:MasterKey is not configured. Generate one with: openssl rand -base64 32");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(masterKeyBase64.Trim());
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException("Encryption:MasterKey is not valid base64.", e);
        }

        if (key.Length != 32)
            throw new InvalidOperationException($"Encryption:MasterKey must decode to 32 bytes, got {key.Length}.");

        _key = key;
        // Separate HMAC key so a hash never doubles as the cipher key.
        _hmacKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, key, 32, info: "bs2-field-hmac"u8.ToArray());
    }

    public string Encrypt(string plaintext)
    {
        if (plaintext.StartsWith(Prefix, StringComparison.Ordinal)) return plaintext;

        var data = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[data.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, data, cipher, tag);

        var blob = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(blob, 0);
        tag.CopyTo(blob, NonceSize);
        cipher.CopyTo(blob, NonceSize + TagSize);
        return Prefix + Convert.ToBase64String(blob);
    }

    public string Decrypt(string ciphertext)
    {
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal)) return ciphertext;

        var blob = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted value is truncated.");

        var nonce = blob.AsSpan(0, NonceSize);
        var tag = blob.AsSpan(NonceSize, TagSize);
        var cipher = blob.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    public string Hash(string value) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(value)));
}
