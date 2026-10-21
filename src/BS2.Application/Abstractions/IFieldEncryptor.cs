namespace BS2.Application.Abstractions;

/// <summary>
/// Application-level encryption for columns marked <see cref="BS2.Domain.Entities.EncryptedAttribute"/>.
/// <see cref="Hash"/> is a keyed, deterministic digest for equality lookups on encrypted values.
/// </summary>
public interface IFieldEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    string Hash(string value);
}
