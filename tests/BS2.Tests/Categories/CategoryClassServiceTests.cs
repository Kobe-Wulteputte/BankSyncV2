using BS2.Application.Categories;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Tests.Transactions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BS2.Tests.Categories;

public class CategoryClassServiceTests
{
    [Fact]
    public async Task Create_trims_and_appends_sort_order()
    {
        using var db = TestDb.Create();
        var service = new CategoryClassService(db);
        var a = await service.CreateAsync(" Fun ", "#fff");
        var b = await service.CreateAsync("Food", null);

        Assert.Equal("Fun", a.Name);
        Assert.Equal(a.SortOrder + 1, b.SortOrder);
    }

    [Fact]
    public async Task Create_rejects_duplicate_name_case_insensitively()
    {
        using var db = TestDb.Create();
        var service = new CategoryClassService(db);
        await service.CreateAsync("Fun", null);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("fUN", null));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(" ", null));
    }

    [Fact]
    public async Task Delete_unclasses_its_categories()
    {
        using var db = TestDb.Create();
        var cls = new CategoryClass { Id = 5, Name = "Fun" };
        var cat = TestDb.Category(1);
        cat.CategoryClassId = 5;
        db.CategoryClasses.Add(cls);
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        var service = new CategoryClassService(db);
        Assert.Equal(1, (await service.ListAsync())[0].CategoryCount);
        await service.DeleteAsync(5);

        Assert.Empty(db.CategoryClasses);
        Assert.Null((await db.Categories.SingleAsync()).CategoryClassId);
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(5));
    }
}
