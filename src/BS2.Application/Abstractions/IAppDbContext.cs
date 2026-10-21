using BS2.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<CategoryClass> CategoryClasses { get; }
    DbSet<Group> Groups { get; }
    DbSet<TransactionGroup> TransactionGroups { get; }
    DbSet<BankConnection> BankConnections { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<ClassificationRun> ClassificationRuns { get; }
    DbSet<PendingAuthorization> PendingAuthorizations { get; }
    DbSet<SyncRun> SyncRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
