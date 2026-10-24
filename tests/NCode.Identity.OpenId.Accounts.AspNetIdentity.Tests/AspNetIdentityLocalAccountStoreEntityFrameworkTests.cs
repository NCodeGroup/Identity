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

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Persistence;
using NCode.Persistence.Stores;
using NCode.Registration;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Tests the adapter against a real <see cref="UserManager{TUser}"/> over ASP.NET Core Identity's Entity Framework
/// store, so the host's default password validators are in play.
/// </summary>
public sealed class AspNetIdentityLocalAccountStoreEntityFrameworkTests : IAsyncLifetime
{
    private const string UserName = "alice";
    private const string LocalAccountId = "user-1";
    private const string ComposedPassword = "caf\u00E9 correct horse battery";
    private const string DecomposedPassword = "cafe\u0301 correct horse battery";

    // Passes Identity's default password validators (a digit, an upper-case letter, a lower-case letter and a symbol).
    private const string HostPassword = "Cafe\u0301-Correct-Horse-1";

    // Passwords that the password policy accepts but that Identity's default validators refuse (no digit and no
    // upper-case letter).
    private static readonly byte[] FirstPassword = "correct horse battery staple"u8.ToArray();
    private static readonly byte[] SecondPassword = "another long passphrase"u8.ToArray();
    private static readonly byte[] ComposedPasswordBytes =
        "caf\u00E9 correct horse battery"u8.ToArray();
    private static readonly byte[] DecomposedPasswordBytes =
        "cafe\u0301 correct horse battery"u8.ToArray();
    private static readonly byte[] ComposedHostPasswordBytes =
        "Caf\u00E9-Correct-Horse-1"u8.ToArray();

    // A hasher that does not normalize, as the host's pages used before the adapter wrapped it; the user manager's
    // hasher normalizes.
    private static readonly PasswordHasher<IdentityUser> RawPasswordHasher = new();

    private static readonly Func<string, CancellationToken, ValueTask<bool>> NotRefused = static (
        _,
        _
    ) => ValueTask.FromResult(false);

    private readonly ServiceProvider _provider;
    private readonly ILocalAccountStore _store;

    public AspNetIdentityLocalAccountStoreEntityFrameworkTests()
    {
        var serviceCollection = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        serviceCollection.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        serviceCollection
            .AddIdentityCore<IdentityUser>()
            .AddEntityFrameworkStores<IdentityDbContext>();
        serviceCollection
            .NewBuilder<StoreManagerLibrary>()
            .AddAspNetIdentityLocalAccountStore<IdentityUser>();

        _provider = serviceCollection.BuildServiceProvider();

        var storeProvider = new Mock<IStoreProvider>(MockBehavior.Strict).Object;
        var factory = _provider.GetRequiredService<Func<IStoreProvider, ILocalAccountStore>>();
        _store = factory(storeProvider);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    #region Helpers

    private static PersistedLocalAccount CreateAccount(
        bool isEnabled = true,
        params (string Type, string Value)[] claims
    ) =>
        new()
        {
            TenantId = string.Empty,
            LocalAccountId = LocalAccountId,
            UserName = UserName,
            Email = "alice@example.com",
            EmailVerified = true,
            IsEnabled = isEnabled,
            Claims = claims
                .Select(claim => new PersistedLocalAccountClaim
                {
                    Type = claim.Type,
                    Value = claim.Value,
                })
                .ToList(),
            ConcurrencyToken = string.Empty,
        };

    // Stores the hash of the password exactly as given, without any normalization.
    private async Task SetStoredPasswordHashAsync(string password)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByIdAsync(LocalAccountId);
        Assert.NotNull(user);
        user.PasswordHash = RawPasswordHasher.HashPassword(user, password);
        var updated = await userManager.UpdateAsync(user);
        Assert.True(updated.Succeeded);
    }

    private async Task<PasswordVerificationResult> VerifyStoredPasswordHashAsync(string password)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByIdAsync(LocalAccountId);
        Assert.NotNull(user);
        Assert.NotNull(user.PasswordHash);
        return RawPasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
    }

    // Checks a password as the host's own ASP.NET Core Identity pages do, through the user manager.
    private async Task<bool> CheckPasswordAsync(string password)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByIdAsync(LocalAccountId);
        Assert.NotNull(user);
        return await userManager.CheckPasswordAsync(user, password);
    }

    // Creates a user as the host's own ASP.NET Core Identity pages do, through the user manager.
    private async Task CreateHostUserAsync(string password)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser(UserName) { Id = LocalAccountId };
        var created = await userManager.CreateAsync(user, password);
        var errors = string.Join("; ", created.Errors.Select(error => error.Description));
        Assert.True(created.Succeeded, errors);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WhenIdentityPasswordValidatorsRefuseThePassword_StoresIt()
    {
        await _store.AddAsync(CreateAccount(), FirstPassword, CancellationToken.None);

        var result = await _store.VerifyCredentialAsync(
            UserName,
            FirstPassword,
            NotRefused,
            CancellationToken.None
        );

        Assert.True(result.IsVerified);
        Assert.Equal(LocalAccountId, result.Account.LocalAccountId);
    }

    #endregion

    #region SetPasswordAsync Tests

    [Fact]
    public async Task SetPasswordAsync_WhenIdentityPasswordValidatorsRefuseThePassword_StoresItAndRotatesTheSecurityStamp()
    {
        await _store.AddAsync(CreateAccount(), FirstPassword, CancellationToken.None);
        var before = await _store.GetByIdOrDefaultAsync(LocalAccountId, CancellationToken.None);
        Assert.NotNull(before);

        await _store.SetPasswordAsync(LocalAccountId, SecondPassword, CancellationToken.None);

        var second = await _store.VerifyCredentialAsync(
            UserName,
            SecondPassword,
            NotRefused,
            CancellationToken.None
        );
        Assert.True(second.IsVerified);
        Assert.NotEqual(before.SecurityStamp, second.Account.SecurityStamp);
        var first = await _store.VerifyCredentialAsync(
            UserName,
            FirstPassword,
            NotRefused,
            CancellationToken.None
        );
        Assert.Null(first.Account);
    }

    #endregion

    #region VerifyCredentialAsync Tests

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheComposedFormIsSubmitted_VerifiesAPasswordSetDecomposed()
    {
        await _store.AddAsync(CreateAccount(), DecomposedPasswordBytes, CancellationToken.None);

        var composed = await _store.VerifyCredentialAsync(
            UserName,
            ComposedPasswordBytes,
            NotRefused,
            CancellationToken.None
        );
        var decomposed = await _store.VerifyCredentialAsync(
            UserName,
            DecomposedPasswordBytes,
            NotRefused,
            CancellationToken.None
        );

        Assert.True(composed.IsVerified);
        Assert.Equal(LocalAccountId, composed.Account.LocalAccountId);
        Assert.True(decomposed.IsVerified);
        Assert.Equal(LocalAccountId, decomposed.Account.LocalAccountId);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheStoredHashIsOfTheDecomposedForm_RehashesTheComposedForm()
    {
        await _store.AddAsync(CreateAccount(), password: null, CancellationToken.None);
        await SetStoredPasswordHashAsync(DecomposedPassword);
        var before = await _store.GetByIdOrDefaultAsync(LocalAccountId, CancellationToken.None);
        Assert.NotNull(before);

        var result = await _store.VerifyCredentialAsync(
            UserName,
            DecomposedPasswordBytes,
            NotRefused,
            CancellationToken.None
        );

        Assert.True(result.IsVerified);
        Assert.Equal(before.SecurityStamp, result.Account.SecurityStamp);
        var verification = await VerifyStoredPasswordHashAsync(ComposedPassword);
        Assert.Equal(PasswordVerificationResult.Success, verification);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenNoUserHasTheUserName_ReturnsNull()
    {
        var result = await _store.VerifyCredentialAsync(
            "nobody",
            FirstPassword,
            NotRefused,
            CancellationToken.None
        );

        Assert.Null(result.Account);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheHostSetTheDecomposedForm_VerifiesTheComposedForm()
    {
        await CreateHostUserAsync(HostPassword);

        var result = await _store.VerifyCredentialAsync(
            UserName,
            ComposedHostPasswordBytes,
            NotRefused,
            CancellationToken.None
        );

        Assert.True(result.IsVerified);
        Assert.Equal(LocalAccountId, result.Account.LocalAccountId);
    }

    #endregion

    #region CheckPasswordAsync Tests

    [Fact]
    public async Task CheckPasswordAsync_WhenTheAdapterStoredTheComposedForm_VerifiesTheDecomposedForm()
    {
        await _store.AddAsync(CreateAccount(), ComposedPasswordBytes, CancellationToken.None);

        var verified = await CheckPasswordAsync(DecomposedPassword);

        Assert.True(verified);
    }

    [Fact]
    public async Task CheckPasswordAsync_WhenTheStoredHashIsOfTheDecomposedForm_ReplacesItWithTheComposedForm()
    {
        await _store.AddAsync(CreateAccount(), password: null, CancellationToken.None);
        await SetStoredPasswordHashAsync(DecomposedPassword);

        var verified = await CheckPasswordAsync(DecomposedPassword);

        Assert.True(verified);
        var verification = await VerifyStoredPasswordHashAsync(ComposedPassword);
        Assert.Equal(PasswordVerificationResult.Success, verification);
    }

    #endregion
}
