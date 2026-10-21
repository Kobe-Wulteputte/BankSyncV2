using BS2.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BS2.Infrastructure.Encryption;

public sealed class EncryptedStringConverter(IFieldEncryptor encryptor) : ValueConverter<string, string>(
    plain => encryptor.Encrypt(plain),
    stored => encryptor.Decrypt(stored));
