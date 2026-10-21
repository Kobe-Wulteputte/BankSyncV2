using BS2.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BS2.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> run without a configured database or key.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=banksync;Username=banksync;Password=banksync",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "banksync"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new NoopFieldEncryptor());
    }

    private sealed class NoopFieldEncryptor : IFieldEncryptor
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string ciphertext) => ciphertext;
        public string Hash(string value) => value;
    }
}
