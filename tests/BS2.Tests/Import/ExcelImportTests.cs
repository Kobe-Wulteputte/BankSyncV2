using BS2.Application.Abstractions;
using BS2.Domain;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using BS2.Infrastructure.Import;
using BS2.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BS2.Tests.Import;

public class ExcelImportTests
{
    private sealed class FakeEncryptor : IFieldEncryptor
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string ciphertext) => ciphertext;
        public string Hash(string value) => value.ToLowerInvariant();
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static MemoryStream Workbook()
    {
        using var wb = new XLWorkbook();
        var s23 = wb.AddWorksheet("2023");
        var s24 = wb.AddWorksheet("2024");
        wb.AddWorksheet("Summary").Cell(1, 1).Value = "ignored";

        void Header(IXLWorksheet ws)
        {
            var h = new[] { "Type", "Amount", "Date", "Account", "Name", "Category", "Group", "Reimbursed", "Description", "Id" };
            for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        }
        void Row(IXLWorksheet ws, int r, string bank, decimal amount, object date, string acc, string name,
            string cat, string group, string reimbursed, string desc, string id)
        {
            ws.Cell(r, 1).Value = bank;
            ws.Cell(r, 2).Value = amount;
            if (date is DateTime d) ws.Cell(r, 3).Value = d; else ws.Cell(r, 3).Value = (string)date;
            ws.Cell(r, 4).Value = acc; ws.Cell(r, 5).Value = name; ws.Cell(r, 6).Value = cat; ws.Cell(r, 7).Value = group;
            ws.Cell(r, 8).Value = reimbursed; ws.Cell(r, 9).Value = desc; ws.Cell(r, 10).Value = id;
        }
        Header(s23); Header(s24);
        Row(s23, 2, "Revolut", -12.5m, new DateTime(2023, 3, 4), "", "Delhaize", "Food and drink (other)", "", "", "lunch", "A1");
        Row(s23, 3, "Argenta", -40m, new DateTime(2023, 3, 5), "BE68539007547034", "Shop", "", "", "", "stuff", "A2");
        Row(s24, 2, "CARD_PAYMENT", -20m, new DateTime(2024, 1, 2), "", "Cafe", "Groceries", "Trip", "", "d", "A3");
        Row(s24, 3, "REVOLUT_REVOGB21", 20m, "05/02/2024", "", "Friend", "Nonsense", "trip", "TRUE", "refund", "A4");
        Row(s24, 4, "Revolut", -1m, new DateTime(2024, 2, 6), "", "Dup", "", "", "", "dup", "A1");
        return Save(wb);
    }

    private static MemoryStream Save(XLWorkbook wb)
    {
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Reader_parses_rows()
    {
        var result = ExcelWorkbookReader.Read(Workbook());

        Assert.Empty(result.Errors);
        Assert.Equal(5, result.Rows.Count);
        Assert.Equal(new DateOnly(2024, 2, 5), result.Rows[3].Date);
        Assert.True(result.Rows[3].Reimbursed);
        Assert.Equal("Food and drink (other)", result.Rows[0].Category);
        Assert.Equal("", result.Rows[1].Category);
        Assert.Equal("Trip", result.Rows[2].Group);
        Assert.Equal(-12.5m, result.Rows[0].Amount);
    }

    private static (ExcelImportService svc, AppDbContext db) Setup(Guid? userId = null)
    {
        var enc = new FakeEncryptor();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, enc);
        db.Categories.AddRange(CategorySeed.Categories.Select(c => new BS2.Domain.Entities.Category
            { Id = c.Id, Code = c.Code, Name = c.Name, Kind = c.Kind, SortOrder = c.SortOrder }));
        db.SaveChanges();
        var options = Microsoft.Extensions.Options.Options.Create(new ImportOptions
        {
            BankAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CARD_PAYMENT"] = "Revolut" }
        });
        return (new ExcelImportService(db, new StaticCurrentUser(userId ?? Guid.NewGuid()), new FakeClock(), enc, options), db);
    }

    [Fact]
    public async Task Import_creates_accounts_groups_and_is_idempotent()
    {
        var (svc, db) = Setup();

        var r = await svc.ImportAsync(Workbook());

        Assert.Equal(5, r.Rows);
        Assert.Equal(4, r.Imported);
        Assert.Equal(1, r.SkippedDuplicates);
        Assert.Equal(1, r.GroupsCreated);
        Assert.Equal(2, r.AccountsCreated); // CARD_PAYMENT and REVOLUT_REVOGB21 both resolve to Revolut
        Assert.Equal(1, r.CategoriesCreated);
        Assert.Contains(r.Errors, e => e.Contains("created category 'Nonsense'"));
        Assert.Contains(r.Errors, e => e.Contains("type 'CARD_PAYMENT' imported under bank 'Revolut'"));
        Assert.Contains(r.Errors, e => e.Contains("type 'REVOLUT_REVOGB21' imported under bank 'Revolut'"));
        var nonsense = await db.Categories.SingleAsync(c => c.Code == "Nonsense");
        Assert.Equal(nonsense.Id, (await db.Transactions.SingleAsync(t => t.ExternalId == "A4")).CategoryId);
        Assert.Equal(new[] { "Argenta", "Revolut" }, db.BankConnections.Select(c => c.BankName).OrderBy(n => n).ToArray());

        Assert.Equal(2, await db.TransactionGroups.CountAsync());
        var food = await db.Transactions.SingleAsync(t => t.ExternalId == "A1");
        Assert.Equal(4, food.CategoryId);
        Assert.Equal(ClassificationSource.Import, food.ClassificationSource);
        var iban = await db.Transactions.SingleAsync(t => t.ExternalId == "A2");
        Assert.Null(iban.CounterpartyIban); // debit: the IBAN is the user's own account, not the counterparty
        Assert.Equal(ClassificationSource.None, iban.ClassificationSource);
        Assert.All(db.BankConnections, c => Assert.Equal(ConnectionStatus.Revoked, c.Status));

        var again = await svc.ImportAsync(Workbook());
        Assert.Equal(0, again.Imported);
        Assert.Equal(0, again.Updated);
        Assert.Equal(5, again.SkippedDuplicates);
        Assert.Equal(0, again.AccountsCreated);
    }

    private static MemoryStream SingleRow(string bank, decimal amount, string account, string id)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("2024");
        var h = new[] { "Type", "Amount", "Date", "Account", "Name", "Category", "Group", "Reimbursed", "Description", "Id" };
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Cell(2, 1).Value = bank; ws.Cell(2, 2).Value = amount; ws.Cell(2, 3).Value = new DateTime(2024, 1, 2);
        ws.Cell(2, 4).Value = account; ws.Cell(2, 5).Value = "Someone"; ws.Cell(2, 9).Value = "d"; ws.Cell(2, 10).Value = id;
        return Save(wb);
    }

    private static BS2.Domain.Entities.Account SeedOwnAccount(AppDbContext db, Guid userId, string iban)
    {
        var conn = new BS2.Domain.Entities.BankConnection { Id = Guid.NewGuid(), UserId = userId, BankName = "Argenta", Country = "BE", Provider = BankProvider.EnableBanking };
        var account = new BS2.Domain.Entities.Account { Id = Guid.NewGuid(), UserId = userId, BankConnectionId = conn.Id, Identifier = iban, IdentifierHash = iban.ToLowerInvariant() };
        db.AddRange(conn, account);
        db.SaveChanges();
        return account;
    }

    [Fact]
    public async Task Debit_account_column_is_own_iban_credit_is_counterparty()
    {
        var userId = Guid.NewGuid();
        var (svc, db) = Setup(userId);
        var own = SeedOwnAccount(db, userId, "BE68539007547034");

        await svc.ImportAsync(SingleRow("Argenta", -10m, "BE68539007547034", "D1"));
        await svc.ImportAsync(SingleRow("Argenta", 10m, "BE68539007547034", "C1"));

        var debit = await db.Transactions.SingleAsync(t => t.ExternalId == "D1");
        Assert.Equal(own.Id, debit.AccountId);
        Assert.Null(debit.CounterpartyIban);
        var credit = await db.Transactions.SingleAsync(t => t.ExternalId == "C1");
        Assert.NotEqual(own.Id, credit.AccountId);
        Assert.Equal("BE68539007547034", credit.CounterpartyIban);
    }

    [Fact]
    public async Task Dedups_against_existing_transactions_of_any_account()
    {
        var userId = Guid.NewGuid();
        var (svc, db) = Setup(userId);
        var own = SeedOwnAccount(db, userId, "BE68539007547034");
        db.Transactions.Add(new BS2.Domain.Entities.Transaction
        {
            Id = Guid.NewGuid(), UserId = userId, AccountId = own.Id, ExternalId = "BANK-1", Amount = -5, Date = new DateOnly(2024, 1, 2)
        });
        db.SaveChanges();

        var r = await svc.ImportAsync(SingleRow("Revolut", -5m, "", "BANK-1"));

        Assert.Equal(0, r.Imported);
        Assert.Equal(1, r.SkippedDuplicates);
    }

    [Fact]
    public async Task Import_applies_labels_to_existing_rows_unless_manually_classified()
    {
        var userId = Guid.NewGuid();
        var (svc, db) = Setup(userId);
        var conn = new BankConnection { Id = Guid.NewGuid(), UserId = userId, BankName = "Revolut", Country = "BE", Provider = BankProvider.EnableBanking };
        var account = new Account { Id = Guid.NewGuid(), UserId = userId, BankConnectionId = conn.Id, Identifier = "X", IdentifierHash = "x" };
        db.BankConnections.Add(conn);
        db.Accounts.Add(account);
        // A1 was classified by the AI (source Ai) -> workbook's "Food and drink (other)" (id 4) must win.
        db.Transactions.Add(new Transaction { Id = Guid.NewGuid(), UserId = userId, AccountId = account.Id, ExternalId = "A1", Amount = -12.5m,
            Date = new DateOnly(2023, 3, 4), CategoryId = 5, ClassificationSource = ClassificationSource.Ai, Source = TransactionSource.EnableBanking });
        // A3 was set by hand in the app -> keep 5, but the workbook group "Trip" is still added.
        db.Transactions.Add(new Transaction { Id = Guid.NewGuid(), UserId = userId, AccountId = account.Id, ExternalId = "A3", Amount = -20m,
            Date = new DateOnly(2024, 1, 2), CategoryId = 5, ClassificationSource = ClassificationSource.Manual, Source = TransactionSource.EnableBanking });
        await db.SaveChangesAsync();

        var r = await svc.ImportAsync(Workbook());

        Assert.Equal(2, r.Updated);
        Assert.Equal(2, r.Imported); // A2, A4 (A1 dup row in the file is unchanged)
        var a1 = await db.Transactions.SingleAsync(t => t.ExternalId == "A1");
        Assert.Equal(4, a1.CategoryId);
        Assert.Equal(ClassificationSource.Import, a1.ClassificationSource);
        var a3 = await db.Transactions.Include(t => t.Groups).ThenInclude(g => g.Group).SingleAsync(t => t.ExternalId == "A3");
        Assert.Equal(5, a3.CategoryId);
        Assert.Equal(ClassificationSource.Manual, a3.ClassificationSource);
        Assert.Contains(a3.Groups, g => g.Group.Name == "Trip");
    }
}
