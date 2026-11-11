// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;

public sealed class LocalAccountStoreTenantScopingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string AccountA = "account-a";
    private const string AccountB = "account-b";
    private const string UserA = "alice";
    private const string UserB = "bob";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TestAmbientTenantAccessor Accessor { get; }
    private LocalAccountStore Store { get; }

    public LocalAccountStoreTenantScopingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        Accessor = Provider.GetRequiredService<TestAmbientTenantAccessor>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new LocalAccountStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            Mock.Of<IPasswordHasher>(),
            DbContext
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedLocalAccount CreateAccount(
        string tenantId,
        string localAccountId,
        string userName
    ) =>
        new()
        {
            TenantId = tenantId,
            LocalAccountId = localAccountId,
            UserName = userName,
            Email = null,
            EmailVerified = false,
            SecurityStamp = "stamp",
            IsEnabled = true,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

    // Seeds one account per tenant unscoped, then detaches so later reads hit the query where the filter applies.
    private async ValueTask SeedAsync(CancellationToken cancellationToken)
    {
        await Store.AddAsync(
            CreateAccount(TenantA, AccountA, UserA),
            password: null,
            cancellationToken
        );
        await Store.AddAsync(
            CreateAccount(TenantB, AccountB, UserB),
            password: null,
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetByUserNameOrDefaultAsync_WhenScopedToTenant_HidesOtherTenants()
    {
        await SeedAsync(CancellationToken.None);

        Accessor.TenantId = TenantA;
        Assert.NotNull(await Store.GetByUserNameOrDefaultAsync(UserA, CancellationToken.None));
        Assert.Null(await Store.GetByUserNameOrDefaultAsync(UserB, CancellationToken.None));

        Accessor.TenantId = TenantB;
        Assert.NotNull(await Store.GetByUserNameOrDefaultAsync(UserB, CancellationToken.None));
        Assert.Null(await Store.GetByUserNameOrDefaultAsync(UserA, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenScopedToTenant_HidesOtherTenants()
    {
        await SeedAsync(CancellationToken.None);

        Accessor.TenantId = TenantA;
        Assert.NotNull(await Store.GetByIdOrDefaultAsync(AccountA, CancellationToken.None));
        Assert.Null(await Store.GetByIdOrDefaultAsync(AccountB, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenUnscoped_SeesAllTenants()
    {
        await SeedAsync(CancellationToken.None);

        Accessor.TenantId = null;
        Assert.NotNull(await Store.GetByIdOrDefaultAsync(AccountA, CancellationToken.None));
        Assert.NotNull(await Store.GetByIdOrDefaultAsync(AccountB, CancellationToken.None));
    }
}
