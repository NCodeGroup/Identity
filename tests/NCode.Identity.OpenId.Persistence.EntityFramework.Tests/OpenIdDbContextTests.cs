// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

public sealed class OpenIdDbContextTests : IDisposable
{
    private SqliteConnection Connection { get; }

    public OpenIdDbContextTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();
    }

    public void Dispose()
    {
        Connection.Dispose();
    }

    #region OnConfiguring Tests

    [Fact]
    public void OnConfiguring_WhenTheProviderIsRelational_SplitsQueries()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));

        using var provider = serviceCollection.BuildServiceProvider();
        using var dbContext = provider.GetRequiredService<OpenIdDbContext>();

        var relational = RelationalOptionsExtension.Extract(
            dbContext.GetService<IDbContextOptions>()
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
}
