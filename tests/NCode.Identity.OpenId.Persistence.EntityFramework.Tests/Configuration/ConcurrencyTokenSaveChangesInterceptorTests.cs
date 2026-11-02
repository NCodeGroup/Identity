// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

public class ConcurrencyTokenSaveChangesInterceptorTests
{
    private sealed class TestEntity
    {
        public int Id { get; set; }
        public required string ConcurrencyToken { get; set; }
        public required string Name { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<TestEntity>();
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        }
    }

    private static TestDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .AddInterceptors(new ConcurrencyTokenSaveChangesInterceptor())
                .Options
        );

    #region SavingChanges Tests

    [Fact]
    public void SavingChanges_WhenEntityAdded_GeneratesConcurrencyToken()
    {
        using var context = CreateContext();
        var entity = new TestEntity
        {
            Id = 1,
            ConcurrencyToken = string.Empty,
            Name = "first",
        };
        context.Add(entity);

        context.SaveChanges();

        Assert.False(string.IsNullOrEmpty(entity.ConcurrencyToken));
        Assert.Equal(32, entity.ConcurrencyToken.Length);
    }

    [Fact]
    public void SavingChanges_WhenEntityModified_ChangesConcurrencyToken()
    {
        using var context = CreateContext();
        var entity = new TestEntity
        {
            Id = 1,
            ConcurrencyToken = string.Empty,
            Name = "first",
        };
        context.Add(entity);
        context.SaveChanges();
        var firstToken = entity.ConcurrencyToken;

        entity.Name = "second";
        context.SaveChanges();

        Assert.NotEqual(firstToken, entity.ConcurrencyToken);
    }

    [Fact]
    public async Task SavingChangesAsync_WhenEntityAdded_GeneratesConcurrencyToken()
    {
        await using var context = CreateContext();
        var entity = new TestEntity
        {
            Id = 1,
            ConcurrencyToken = string.Empty,
            Name = "first",
        };
        context.Add(entity);

        await context.SaveChangesAsync();

        Assert.False(string.IsNullOrEmpty(entity.ConcurrencyToken));
        Assert.Equal(32, entity.ConcurrencyToken.Length);
    }

    [Fact]
    public async Task SavingChangesAsync_WhenEntityModified_ChangesConcurrencyToken()
    {
        await using var context = CreateContext();
        var entity = new TestEntity
        {
            Id = 1,
            ConcurrencyToken = string.Empty,
            Name = "first",
        };
        context.Add(entity);
        await context.SaveChangesAsync();
        var firstToken = entity.ConcurrencyToken;

        entity.Name = "second";
        await context.SaveChangesAsync();

        Assert.NotEqual(firstToken, entity.ConcurrencyToken);
    }

    [Fact]
    public async Task SavingChangesAsync_WhenEntityAddedWithToken_KeepsCallerToken()
    {
        await using var context = CreateContext();
        var entity = new TestEntity
        {
            Id = 1,
            ConcurrencyToken = "caller-token",
            Name = "first",
        };
        context.Add(entity);

        await context.SaveChangesAsync();

        Assert.Equal("caller-token", entity.ConcurrencyToken);
    }

    #endregion
}
