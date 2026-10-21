using BS2.Domain;
using BS2.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace BS2.Infrastructure.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Auth0Subject).HasMaxLength(256).IsRequired();
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(256);
        b.HasIndex(x => x.Auth0Subject).IsUnique();
    }
}

public sealed class CategoryClassConfiguration : IEntityTypeConfiguration<CategoryClass>
{
    public void Configure(EntityTypeBuilder<CategoryClass> b)
    {
        b.ToTable("category_classes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 1000);
        b.Property(x => x.Name).HasMaxLength(64).IsRequired();
        b.Property(x => x.Color).HasMaxLength(16);
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(CategorySeed.Classes);
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.HasKey(x => x.Id);
        // Seed rows occupy 1..35 with explicit ids; the identity sequence must start above them
        // or the first user-created category collides with the seed.
        b.Property(x => x.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 1000);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Color).HasMaxLength(16);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne(x => x.CategoryClass).WithMany(c => c.Categories).HasForeignKey(x => x.CategoryClassId).OnDelete(DeleteBehavior.SetNull);
        b.HasData(CategorySeed.Categories.Select(c => new Category
        {
            Id = c.Id, Code = c.Code, Name = c.Name, Kind = c.Kind, SortOrder = c.SortOrder, IsActive = true, CategoryClassId = c.CategoryClassId
        }));
    }
}

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        b.ToTable("groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Color).HasMaxLength(16);
        b.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
    }
}

public sealed class TransactionGroupConfiguration : IEntityTypeConfiguration<TransactionGroup>
{
    public void Configure(EntityTypeBuilder<TransactionGroup> b)
    {
        b.ToTable("transaction_groups");
        b.HasKey(x => new { x.TransactionId, x.GroupId });
        b.HasOne(x => x.Transaction).WithMany(t => t.Groups).HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Group).WithMany(g => g.Transactions).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.GroupId);
    }
}

public sealed class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> b)
    {
        b.ToTable("bank_connections");
        b.HasKey(x => x.Id);
        b.Property(x => x.BankName).HasMaxLength(128).IsRequired();
        b.Property(x => x.Country).HasMaxLength(2).IsRequired();
        b.Property(x => x.PsuType).HasMaxLength(16).IsRequired();
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.LastSyncError).HasMaxLength(2000);
        b.Ignore(x => x.ConfiguredIbans);
        b.Ignore(x => x.SyncsAllAccounts);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.UserId, x.Provider, x.BankName }).IsUnique();
    }
}

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.IdentifierHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(128);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.HasOne(x => x.BankConnection).WithMany(c => c.Accounts).HasForeignKey(x => x.BankConnectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.IdentifierHash }).IsUnique();
    }
}

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("transactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalId).HasMaxLength(256).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ClassificationSource).HasConversion<string>().HasMaxLength(16);
        b.HasOne(x => x.Account).WithMany(a => a.Transactions).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.AccountId, x.ExternalId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Date });
        b.HasIndex(x => new { x.UserId, x.CategoryId });
    }
}

public sealed class ClassificationRunConfiguration : IEntityTypeConfiguration<ClassificationRun>
{
    public void Configure(EntityTypeBuilder<ClassificationRun> b)
    {
        b.ToTable("classification_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Model).HasMaxLength(128).IsRequired();
        b.Property(x => x.PredictedCategoryCode).HasMaxLength(64);
        b.Property(x => x.AlternativesJson).HasColumnType("jsonb");
        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(16);
        b.HasOne(x => x.Transaction).WithMany(t => t.ClassificationRuns).HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.PredictedCategory).WithMany().HasForeignKey(x => x.PredictedCategoryId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.TransactionId, x.CreatedAt });
    }
}

public sealed class PendingAuthorizationConfiguration : IEntityTypeConfiguration<PendingAuthorization>
{
    public void Configure(EntityTypeBuilder<PendingAuthorization> b)
    {
        b.ToTable("pending_authorizations");
        b.HasKey(x => x.Id);
        b.Property(x => x.State).HasMaxLength(64).IsRequired();
        b.HasOne(x => x.BankConnection).WithMany().HasForeignKey(x => x.BankConnectionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.State).IsUnique();
    }
}

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> b)
    {
        b.ToTable("sync_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Error).HasMaxLength(4000);
        b.Property(x => x.DetailsJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.UserId, x.StartedAt });
        // One running sync per user, enforced by the database so a manual and a scheduled run cannot race.
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("status = 'Running'").HasDatabaseName("ux_sync_runs_user_running");
    }
}
