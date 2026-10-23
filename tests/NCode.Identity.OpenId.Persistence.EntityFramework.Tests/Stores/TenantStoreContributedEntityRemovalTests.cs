#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.ComponentModel.DataAnnotations;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// A package's tenant-owned entity, such as a local account, is erased with its tenant (ADR-0212). The contributed
// entity changes the model, which Entity Framework caches per context type, so this context disables that caching.
public sealed class TenantStoreContributedEntityRemovalTests : IDisposable
{
    private const string TenantOneId = "tenant-1";
    private const string TenantTwoId = "tenant-2";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;

    public TenantStoreContributedEntityRemovalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOpenIdModelContributor, TestTenantNoteModelContributor>()
        );
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseSqlite(_connection).EnableServiceProviderCaching(false)
        );
        _provider = serviceCollection.BuildServiceProvider();

        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _dbContext.Database.EnsureCreated();

        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
        _connection.Dispose();
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
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _dbContext.SeedTenant(idGenerator, TenantOneId);
        _dbContext.SeedTenant(idGenerator, TenantTwoId);

        foreach (var tenantId in new[] { TenantOneId, TenantTwoId })
        {
            _dbContext
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

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveAsync(TenantOneId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);

        var note = Assert.Single(await _dbContext.Set<TestTenantNoteEntity>().ToListAsync());
        Assert.Equal(TenantTwoId, note.NormalizedTenantId);
    }

    #endregion
}
