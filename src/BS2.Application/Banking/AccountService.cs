using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Banking;

public sealed class AccountService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<AccountDto[]> ListAsync(CancellationToken ct = default)
    {
        var accounts = await db.Accounts.Include(a => a.BankConnection)
            .Where(a => a.UserId == currentUser.UserId)
            .ToListAsync(ct);

        var counts = await db.Transactions
            .Where(t => t.UserId == currentUser.UserId)
            .GroupBy(t => t.AccountId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return accounts
            .OrderBy(a => a.BankConnection.BankName).ThenBy(a => a.Identifier)
            .Select(a => ToDto(a, a.BankConnection.BankName, counts.GetValueOrDefault(a.Id)))
            .ToArray();
    }

    public async Task<AccountDto> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var account = await db.Accounts.Include(a => a.BankConnection)
                          .SingleOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.UserId, ct)
                      ?? throw new NotFoundException("Account not found.");

        if (request.DisplayName is not null)
            account.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        account.IsActive = request.IsActive;
        account.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        var count = await db.Transactions.CountAsync(t => t.AccountId == id, ct);
        return ToDto(account, account.BankConnection.BankName, count);
    }

    internal static AccountDto ToDto(Account a, string bankName, int transactionCount) =>
        new(a.Id, a.BankConnectionId, bankName, a.Identifier, a.DisplayName, a.Currency, a.IsActive, transactionCount);
}
