#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;

public sealed class LocalAccountStoreTenantScopingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string AccountA = "account-a";
    private const string AccountB = "account-b";
    private const string UserA = "alice";
    private const string UserB = "bob";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TestAmbientTenantAccessor _accessor;
    private readonly LocalAccountStore _store;

    public LocalAccountStoreTenantScopingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _accessor = _provider.GetRequiredService<TestAmbientTenantAccessor>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new LocalAccountStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
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
            PasswordHash = null,
            SecurityStamp = "stamp",
            IsEnabled = true,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

    // Seeds one account per tenant unscoped, then detaches so later reads hit the query where the filter applies.
    private async Task SeedAsync()
    {
        await _store.AddAsync(CreateAccount(TenantA, AccountA, UserA), CancellationToken.None);
        await _store.AddAsync(CreateAccount(TenantB, AccountB, UserB), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetByUserNameOrDefaultAsync_WhenScopedToTenant_HidesOtherTenants()
    {
        await SeedAsync();

        _accessor.TenantId = TenantA;
        Assert.NotNull(await _store.GetByUserNameOrDefaultAsync(UserA, CancellationToken.None));
        Assert.Null(await _store.GetByUserNameOrDefaultAsync(UserB, CancellationToken.None));

        _accessor.TenantId = TenantB;
        Assert.NotNull(await _store.GetByUserNameOrDefaultAsync(UserB, CancellationToken.None));
        Assert.Null(await _store.GetByUserNameOrDefaultAsync(UserA, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenScopedToTenant_HidesOtherTenants()
    {
        await SeedAsync();

        _accessor.TenantId = TenantA;
        Assert.NotNull(await _store.GetByIdOrDefaultAsync(AccountA, CancellationToken.None));
        Assert.Null(await _store.GetByIdOrDefaultAsync(AccountB, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenUnscoped_SeesAllTenants()
    {
        await SeedAsync();

        _accessor.TenantId = null;
        Assert.NotNull(await _store.GetByIdOrDefaultAsync(AccountA, CancellationToken.None));
        Assert.NotNull(await _store.GetByIdOrDefaultAsync(AccountB, CancellationToken.None));
    }
}
