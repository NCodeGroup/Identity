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

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;

public sealed class LocalAccountStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly LocalAccountStore _store;

    public LocalAccountStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new LocalAccountStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    #region Helpers

    private static PersistedLocalAccount CreateAccount(
        string localAccountId,
        string userName,
        string? email = null,
        string? passwordHash = null,
        bool isEnabled = true,
        params PersistedLocalAccountClaim[] claims
    ) =>
        new()
        {
            TenantId = TenantId,
            LocalAccountId = localAccountId,
            UserName = userName,
            Email = email,
            EmailVerified = false,
            PasswordHash = passwordHash,
            SecurityStamp = "stamp",
            IsEnabled = isEnabled,
            Claims = claims,
            ConcurrencyToken = string.Empty,
        };

    private async Task AddAndDetachAsync(PersistedLocalAccount account)
    {
        await _store.AddAsync(account, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WhenPersisted_RoundTripsById()
    {
        await AddAndDetachAsync(CreateAccount("account-1", "alice", email: "alice@example.com"));

        var loaded = await _store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("account-1", loaded.LocalAccountId);
        Assert.Equal("alice", loaded.UserName);
        Assert.Equal("alice@example.com", loaded.Email);
        Assert.True(loaded.IsEnabled);
        Assert.NotEmpty(loaded.ConcurrencyToken);
    }

    [Fact]
    public async Task AddAsync_WhenPersisted_StampsOwningTenant()
    {
        await AddAndDetachAsync(CreateAccount("account-1", "alice"));

        var loaded = await _store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(TenantId, loaded.TenantId);
    }

    [Fact]
    public async Task AddAsync_WhenClaimsPresent_PersistsAndMapsClaims()
    {
        await AddAndDetachAsync(
            CreateAccount(
                "account-1",
                "alice",
                claims: new PersistedLocalAccountClaim { Type = "role", Value = "admin" }
            )
        );

        var loaded = await _store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

        Assert.NotNull(loaded);
        var claim = Assert.Single(loaded.Claims);
        Assert.Equal("role", claim.Type);
        Assert.Equal("admin", claim.Value);
    }

    #endregion

    #region GetByIdOrDefaultAsync Tests

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenCaseDiffers_IsCaseInsensitive()
    {
        await AddAndDetachAsync(CreateAccount("Account-1", "alice"));

        var loaded = await _store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Account-1", loaded.LocalAccountId);
    }

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        var loaded = await _store.GetByIdOrDefaultAsync("missing", CancellationToken.None);

        Assert.Null(loaded);
    }

    #endregion

    #region GetByUserNameOrDefaultAsync Tests

    [Fact]
    public async Task GetByUserNameOrDefaultAsync_WhenCaseDiffers_IsCaseInsensitive()
    {
        await AddAndDetachAsync(CreateAccount("account-1", "Alice"));

        var loaded = await _store.GetByUserNameOrDefaultAsync("alice", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded.UserName);
    }

    [Fact]
    public async Task GetByUserNameOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        var loaded = await _store.GetByUserNameOrDefaultAsync("nobody", CancellationToken.None);

        Assert.Null(loaded);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenAccountExists_UpdatesMutableFields()
    {
        await AddAndDetachAsync(CreateAccount("account-1", "alice", isEnabled: true));

        var account = await _store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);
        Assert.NotNull(account);
        account.UserName = "alice-renamed";
        account.Email = "renamed@example.com";
        account.IsEnabled = false;

        await _store.UpdateAsync(account, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _store.GetByUserNameOrDefaultAsync(
            "alice-renamed",
            CancellationToken.None
        );
        Assert.NotNull(loaded);
        Assert.Equal("renamed@example.com", loaded.Email);
        Assert.False(loaded.IsEnabled);
    }

    [Fact]
    public async Task UpdateAsync_WhenAccountMissing_Throws()
    {
        var account = CreateAccount("missing", "ghost");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.UpdateAsync(account, CancellationToken.None)
        );
    }

    #endregion
}
