// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;

public sealed class LocalAccountStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private static Func<string, CancellationToken, ValueTask<bool>> NotRefused { get; } =
        static (_, _) => ValueTask.FromResult(false);

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private LocalAccountStore Store { get; }
    private PlainPasswordHasher Hasher { get; } = new();

    public LocalAccountStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
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

    #region Helpers

    private static PersistedLocalAccount CreateAccount(
        string localAccountId,
        string userName,
        string? email = null,
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
            SecurityStamp = "stamp",
            IsEnabled = isEnabled,
            Claims = claims,
            ConcurrencyToken = string.Empty,
        };

    private async ValueTask AddAndDetachAsync(
        PersistedLocalAccount account,
        CancellationToken cancellationToken
    )
    {
        await Store.AddAsync(account, password: null, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private LocalAccountStore CreateStoreWithPasswords() =>
        new(
            Mock.Of<IStoreProvider>(),
            Provider.GetRequiredService<IIdGenerator<long>>(),
            Hasher,
            DbContext
        );

    private async ValueTask AddWithPasswordAndDetachAsync(
        LocalAccountStore store,
        PersistedLocalAccount account,
        string password,
        CancellationToken cancellationToken
    )
    {
        await store.AddAsync(account, SecureEncoding.UTF8.GetBytes(password), cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private sealed class PlainPasswordHasher : IPasswordHasher
    {
        public string DummyPasswordHash { get; set; } = "plain:dummy";

        public List<string> VerifiedHashes { get; } = [];

        public string HashPassword(ReadOnlySpan<byte> password) =>
            "plain:" + Convert.ToBase64String(password);

        public PasswordVerificationResult VerifyHashedPassword(
            string hashedPassword,
            ReadOnlySpan<byte> password
        )
        {
            VerifiedHashes.Add(hashedPassword);
            return string.Equals(hashedPassword, HashPassword(password), StringComparison.Ordinal)
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WhenPersisted_RoundTripsById()
    {
        await AddAndDetachAsync(
            CreateAccount("account-1", "alice", email: "alice@example.com"),
            CancellationToken.None
        );

        var loaded = await Store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

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
        await AddAndDetachAsync(CreateAccount("account-1", "alice"), CancellationToken.None);

        var loaded = await Store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

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
            ),
            CancellationToken.None
        );

        var loaded = await Store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

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
        await AddAndDetachAsync(CreateAccount("Account-1", "alice"), CancellationToken.None);

        var loaded = await Store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Account-1", loaded.LocalAccountId);
    }

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        var loaded = await Store.GetByIdOrDefaultAsync("missing", CancellationToken.None);

        Assert.Null(loaded);
    }

    #endregion

    #region GetByUserNameOrDefaultAsync Tests

    [Fact]
    public async Task GetByUserNameOrDefaultAsync_WhenCaseDiffers_IsCaseInsensitive()
    {
        await AddAndDetachAsync(CreateAccount("account-1", "Alice"), CancellationToken.None);

        var loaded = await Store.GetByUserNameOrDefaultAsync("alice", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded.UserName);
    }

    [Fact]
    public async Task GetByUserNameOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        var loaded = await Store.GetByUserNameOrDefaultAsync("nobody", CancellationToken.None);

        Assert.Null(loaded);
    }

    #endregion

    #region VerifyCredentialAsync Tests

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheAccountIsDisabled_ReturnsTheAccount()
    {
        var store = CreateStoreWithPasswords();
        await AddWithPasswordAndDetachAsync(
            store,
            CreateAccount("account-1", "alice", isEnabled: false),
            "secret",
            CancellationToken.None
        );

        var result = await store.VerifyCredentialAsync(
            "alice",
            SecureEncoding.UTF8.GetBytes("secret"),
            NotRefused,
            CancellationToken.None
        );

        Assert.True(result.IsVerified);
        Assert.Equal("account-1", result.Account.LocalAccountId);
        Assert.False(result.Account.IsEnabled);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenThePasswordIsWrong_ReturnsNull()
    {
        var store = CreateStoreWithPasswords();
        await AddWithPasswordAndDetachAsync(
            store,
            CreateAccount("account-1", "alice", isEnabled: false),
            "secret",
            CancellationToken.None
        );

        var result = await store.VerifyCredentialAsync(
            "alice",
            SecureEncoding.UTF8.GetBytes("wrong"),
            NotRefused,
            CancellationToken.None
        );

        Assert.Null(result.Account);
        Assert.NotEqual("plain:dummy", Assert.Single(Hasher.VerifiedHashes));
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheAccountHasNoPassword_VerifiesAgainstTheDummyHash()
    {
        var store = CreateStoreWithPasswords();
        await AddAndDetachAsync(CreateAccount("account-1", "alice"), CancellationToken.None);

        var result = await store.VerifyCredentialAsync(
            "alice",
            SecureEncoding.UTF8.GetBytes("secret"),
            NotRefused,
            CancellationToken.None
        );

        Assert.Null(result.Account);
        Assert.Equal("plain:dummy", Assert.Single(Hasher.VerifiedHashes));
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenNoAccountHasTheUserName_VerifiesAgainstTheDummyHash()
    {
        var store = CreateStoreWithPasswords();

        var result = await store.VerifyCredentialAsync(
            "nobody",
            SecureEncoding.UTF8.GetBytes("secret"),
            NotRefused,
            CancellationToken.None
        );

        Assert.Null(result.Account);
        Assert.Equal("plain:dummy", Assert.Single(Hasher.VerifiedHashes));
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheDummyHashMatches_ReturnsNull()
    {
        Hasher.DummyPasswordHash = Hasher.HashPassword("secret"u8);
        var store = CreateStoreWithPasswords();

        var result = await store.VerifyCredentialAsync(
            "nobody",
            SecureEncoding.UTF8.GetBytes("secret"),
            NotRefused,
            CancellationToken.None
        );

        Assert.Null(result.Account);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheAccountHasNoPasswordAndTheDummyHashMatches_ReturnsNull()
    {
        Hasher.DummyPasswordHash = Hasher.HashPassword("secret"u8);
        var store = CreateStoreWithPasswords();
        await AddAndDetachAsync(CreateAccount("account-1", "alice"), CancellationToken.None);

        var result = await store.VerifyCredentialAsync(
            "alice",
            SecureEncoding.UTF8.GetBytes("secret"),
            NotRefused,
            CancellationToken.None
        );

        Assert.Null(result.Account);
        Assert.Equal(Hasher.DummyPasswordHash, Assert.Single(Hasher.VerifiedHashes));
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheGateRefuses_VerifiesTheDummyHashInsteadOfTheStoredHash()
    {
        var store = CreateStoreWithPasswords();
        await AddWithPasswordAndDetachAsync(
            store,
            CreateAccount("account-1", "alice"),
            "secret",
            CancellationToken.None
        );

        var result = await store.VerifyCredentialAsync(
            "alice",
            SecureEncoding.UTF8.GetBytes("secret"),
            static (_, _) => ValueTask.FromResult(true),
            CancellationToken.None
        );

        Assert.True(result.IsRefused);
        Assert.Null(result.Account);
        Assert.Equal("plain:dummy", Assert.Single(Hasher.VerifiedHashes));
        Assert.Equal("alice", result.UserName);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheCaseDiffers_AsksTheGateAboutTheStoredUserName()
    {
        var store = CreateStoreWithPasswords();
        await AddWithPasswordAndDetachAsync(
            store,
            CreateAccount("account-1", "Alice"),
            "secret",
            CancellationToken.None
        );
        var askedUserNames = new List<string>();

        var result = await store.VerifyCredentialAsync(
            "ALICE",
            SecureEncoding.UTF8.GetBytes("secret"),
            (userName, _) =>
            {
                askedUserNames.Add(userName);
                return ValueTask.FromResult(false);
            },
            CancellationToken.None
        );

        Assert.Equal("Alice", Assert.Single(askedUserNames));
        Assert.Equal("Alice", result.UserName);
        Assert.True(result.IsVerified);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenNoAccountHasTheUserName_AsksTheGateAboutTheSubmittedUserName()
    {
        var store = CreateStoreWithPasswords();
        var askedUserNames = new List<string>();

        var result = await store.VerifyCredentialAsync(
            "Nobody",
            SecureEncoding.UTF8.GetBytes("secret"),
            (userName, _) =>
            {
                askedUserNames.Add(userName);
                return ValueTask.FromResult(false);
            },
            CancellationToken.None
        );

        Assert.Equal("Nobody", Assert.Single(askedUserNames));
        Assert.Equal("Nobody", result.UserName);
        Assert.Equal("plain:dummy", Assert.Single(Hasher.VerifiedHashes));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenAccountExists_UpdatesMutableFields()
    {
        await AddAndDetachAsync(
            CreateAccount("account-1", "alice", isEnabled: true),
            CancellationToken.None
        );

        var account = await Store.GetByIdOrDefaultAsync("account-1", CancellationToken.None);
        Assert.NotNull(account);
        account.UserName = "alice-renamed";
        account.Email = "renamed@example.com";
        account.IsEnabled = false;

        await Store.UpdateAsync(account, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var loaded = await Store.GetByUserNameOrDefaultAsync(
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
            await Store.UpdateAsync(account, CancellationToken.None)
        );
    }

    #endregion
}
