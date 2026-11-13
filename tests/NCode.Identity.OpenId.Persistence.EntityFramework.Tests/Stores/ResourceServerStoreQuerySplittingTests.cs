// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates that a relational provider runs a query that includes several collections as split queries. The warning
// EF raises for such a query as a single query is configured to throw, so a single-query read fails the test.
public sealed class ResourceServerStoreQuerySplittingTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";
    private const string Identifier = "https://api.example.com";

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ResourceServerStore ResourceServerStore { get; }

    public ResourceServerStoreQuerySplittingTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options
                .UseSqlite(Connection)
                .ConfigureWarnings(warnings =>
                    warnings.Throw(RelationalEventId.MultipleCollectionIncludeWarning)
                )
        );
        Provider = serviceCollection.BuildServiceProvider();

        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        DbContext.Database.EnsureCreated();
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");

        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        ResourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
        Connection.Dispose();
    }

    #region Helpers

    private static PersistedSecret CreateSecret(string secretId) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = "encoded-value",
        }.Build();

    private static PersistedScope CreateScope(string value) =>
        new()
        {
            Value = value,
            Description = $"{value} description",
            IsSystem = false,
        };

    private static PersistedTenant CreateTenant() =>
        new PersistedTenantBuilder { TenantId = TenantId, DisplayName = "Tenant One" }.Build();

    private static PersistedResourceServer CreateResourceServer() =>
        new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Identifier = Identifier,
            Scopes = [CreateScope("read:x"), CreateScope("write:x")],
            Secrets = [CreateSecret("rs-1-secret")],
        }.Build();

    #endregion

    #region OnConfiguring Tests

    [Fact]
    public void OnConfiguring_WhenTheProviderIsRelational_SplitsQueries()
    {
        var relational = RelationalOptionsExtension.Extract(
            DbContext.GetService<IDbContextOptions>()
        );

        Assert.Equal(QuerySplittingBehavior.SplitQuery, relational.QuerySplittingBehavior);
    }

    [Fact]
    public void OnConfiguring_WhenTheHostChoseAQuerySplittingBehavior_KeepsIt()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseSqlite(
                Connection,
                sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
            )
        );

        using var provider = serviceCollection.BuildServiceProvider();
        using var dbContext = provider.GetRequiredService<OpenIdDbContext>();

        var relational = RelationalOptionsExtension.Extract(
            dbContext.GetService<IDbContextOptions>()
        );

        Assert.Equal(QuerySplittingBehavior.SingleQuery, relational.QuerySplittingBehavior);
    }

    #endregion

    #region GetByIdentifierOrDefaultAsync Tests

    [Fact]
    public async Task GetByIdentifierOrDefaultAsync_WhenTheResourceServerHasScopesAndSecrets_ReadsThemWithoutTheWarning()
    {
        await TenantStore.AddAsync(CreateTenant(), CancellationToken.None);
        await DbContext.SaveChangesAsync();

        await ResourceServerStore.AddAsync(CreateResourceServer(), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var resourceServer = await ResourceServerStore.GetByIdentifierOrDefaultAsync(
            Identifier,
            CancellationToken.None
        );

        Assert.NotNull(resourceServer);
        Assert.Equal(
            ["read:x", "write:x"],
            resourceServer.Scopes.Select(scope => scope.Value).Order()
        );
        var secret = Assert.Single(resourceServer.Secrets.Value);
        Assert.Equal("rs-1-secret", secret.SecretId);
    }

    #endregion
}
