// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// A package's tenant-owned entity, such as a local account, is erased with its tenant (ADR-1212). The contributed
// entity changes the model, which Entity Framework caches per context type, so this context disables that caching.
public sealed class TenantStoreContributedEntityRemovalTests : IDisposable
{
    private const string TenantOneId = "tenant-1";
    private const string TenantTwoId = "tenant-2";

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore Store { get; }

    public TenantStoreContributedEntityRemovalTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOpenIdModelContributor, TestTenantNoteModelContributor>()
        );
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseSqlite(Connection).EnableServiceProviderCaching(false)
        );
        Provider = serviceCollection.BuildServiceProvider();

        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        DbContext.Database.EnsureCreated();

        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
        Connection.Dispose();
    }

    #region Helpers

    private sealed class TestTenantNoteEntity : ISupportTenantEntity
    {
        [Key]
        public required long Id { get; init; }

        public required string NormalizedTenantId { get; init; }

        public required string Text { get; init; }
    }

    private sealed class TestTenantNoteModelContributor : IOpenIdModelContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<TestTenantNoteEntity>()
                .ToTable("TestTenantNotes")
                .Property(note => note.Id)
                .ValueGeneratedNever();
        }
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenAnotherPackageContributesATenantOwnedEntity_RemovesItsRows()
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        DbContext.SeedTenant(idGenerator, TenantOneId);
        DbContext.SeedTenant(idGenerator, TenantTwoId);

        foreach (var tenantId in new[] { TenantOneId, TenantTwoId })
        {
            DbContext
                .Set<TestTenantNoteEntity>()
                .Add(
                    new TestTenantNoteEntity
                    {
                        Id = idGenerator.CreateId(),
                        NormalizedTenantId = tenantId,
                        Text = $"{tenantId}-note",
                    }
                );
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveAsync(TenantOneId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);

        var note = Assert.Single(await DbContext.Set<TestTenantNoteEntity>().ToListAsync());
        Assert.Equal(TenantTwoId, note.NormalizedTenantId);
    }

    #endregion
}
