using BS2.Application.Abstractions;
using BS2.Domain.Entities;
using BS2.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;

namespace BS2.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, IFieldEncryptor encryptor) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryClass> CategoryClasses => Set<CategoryClass>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<TransactionGroup> TransactionGroups => Set<TransactionGroup>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ClassificationRun> ClassificationRuns => Set<ClassificationRun>();
    public DbSet<PendingAuthorization> PendingAuthorizations => Set<PendingAuthorization>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("banksync");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Every [Encrypted] string property gets the converter. Done here rather than per
        // configuration so adding a sensitive column is one attribute, not two edits.
        var converter = new EncryptedStringConverter(encryptor);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            var member = property.PropertyInfo;
            if (member is null || property.ClrType != typeof(string)) continue;
            if (member.GetCustomAttributes(typeof(EncryptedAttribute), inherit: true).Length == 0) continue;

            property.SetValueConverter(converter);
            // Ciphertext is ~1.4x plaintext plus overhead, and Postgres text is unbounded anyway.
            property.SetMaxLength(null);
        }
    }
}
