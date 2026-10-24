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

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

public sealed class AspNetIdentityLocalAccountStoreTests : IDisposable
{
    private const string UserName = "alice";
    private const string LocalAccountId = "user-1";
    private const string Password = "correct-horse";
    private const string ComposedPassword = "caf\u00E9-correct-horse";
    private const string DecomposedPassword = "cafe\u0301-correct-horse";
    private const string PasswordHash = "password-hash";
    private const string StoredPasswordHash = "stored-hash";
    private const string RehashedPasswordHash = "rehashed-hash";
    private const string DummyPasswordHash = "dummy-hash";
    private const string SecurityStamp = "stamp";
    private const string ConcurrencyToken = "ctoken";
    private static readonly byte[] PasswordBytes = "correct-horse"u8.ToArray();
    private static readonly byte[] DecomposedPasswordBytes = "cafe\u0301-correct-horse"u8.ToArray();
    private static readonly Claim RoleClaim = new("role", "admin");

    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    public void Dispose() => _mocks.Verify();

    #region Helpers

    private (
        AspNetIdentityLocalAccountStore<IdentityUser> Store,
        Mock<UserManager<IdentityUser>> UserManager
    ) CreateStore(
        IPasswordHasher<IdentityUser>? passwordHasher = null,
        IUserStore<IdentityUser>? userStore = null
    )
    {
        // UserManager's constructor needs only a user store and the password hasher (index 2); the remaining
        // dependencies are unused by this adapter.
        object?[] constructorArguments =
        [
            Mock.Of<IUserStore<IdentityUser>>(),
            null,
            passwordHasher,
            null,
            null,
            null,
            null,
            null,
            null,
        ];
        var userManager = _mocks.Create<UserManager<IdentityUser>>((object[])constructorArguments);

        // UserManager's constructor assigns its virtual Logger property; allow that set on the strict mock.
        userManager.SetupSet(x => x.Logger = It.IsAny<ILogger>());

        var serviceProvider = _mocks.Create<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(UserManager<IdentityUser>)))
            .Returns(userManager.Object)
            .Verifiable();

        if (userStore is not null)
        {
            serviceProvider
                .Setup(x => x.GetService(typeof(IUserStore<IdentityUser>)))
                .Returns(userStore)
                .Verifiable();
        }

        // The store opens a DI scope per call to resolve the scoped UserManager; mock the plumbing so the strict
        // UserManager mock is never disposed (a non-virtual Dispose would escape the mock).
        var scope = _mocks.Create<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object).Verifiable();
        scope.Setup(x => x.Dispose()).Verifiable();

        var scopeFactory = _mocks.Create<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object).Verifiable();

        // The unit-of-work store provider is only used for the IStoreProvider delegation, never by these operations.
        var storeProvider = _mocks.Create<IStoreProvider>();

        return (
            new AspNetIdentityLocalAccountStore<IdentityUser>(
                scopeFactory.Object,
                storeProvider.Object,
                new AspNetIdentityDummyPasswordHash<IdentityUser>()
            ),
            userManager
        );
    }

    private Mock<IPasswordHasher<IdentityUser>> CreatePasswordHasher() =>
        _mocks.Create<IPasswordHasher<IdentityUser>>();

    private Mock<IUserPasswordStore<IdentityUser>> CreatePasswordStore() =>
        _mocks.Create<IUserPasswordStore<IdentityUser>>();

    private static void SetupDummyPasswordHash(
        Mock<IPasswordHasher<IdentityUser>> passwordHasher
    ) =>
        passwordHasher
            .Setup(x =>
                x.HashPassword(
                    It.Is<IdentityUser>(user => user.Id != LocalAccountId),
                    It.IsAny<string>()
                )
            )
            .Returns(DummyPasswordHash)
            .Verifiable();

    private static IdentityUser CreateUser() =>
        new(UserName)
        {
            Id = LocalAccountId,
            Email = null,
            EmailConfirmed = false,
            SecurityStamp = SecurityStamp,
            ConcurrencyStamp = ConcurrencyToken,
        };

    private static void SetupMapping(Mock<UserManager<IdentityUser>> userManager, IdentityUser user)
    {
        userManager.Setup(x => x.GetClaimsAsync(user)).ReturnsAsync([RoleClaim]).Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true);
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
    }

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

    #endregion

    #region VerifyCredentialAsync Tests

    [Fact]
    public async Task VerifyCredentialAsync_WhenUserNotFound_VerifiesAgainstTheDummyHash()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        userManager
            .Setup(x => x.FindByNameAsync(UserName))
            .ReturnsAsync((IdentityUser?)null)
            .Verifiable();
        passwordHasher
            .Setup(x =>
                x.VerifyHashedPassword(It.IsAny<IdentityUser>(), DummyPasswordHash, Password)
            )
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();

        var result = await store.VerifyCredentialAsync(
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenLockedOut_VerifiesAgainstTheDummyHash()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true).Verifiable();
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true).Verifiable();
        passwordHasher
            .Setup(x =>
                x.VerifyHashedPassword(It.IsAny<IdentityUser>(), DummyPasswordHash, Password)
            )
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();

        var result = await store.VerifyCredentialAsync(
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenPasswordInvalid_ReturnsNull()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true).Verifiable();
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredPasswordHash)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, Password))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();

        var result = await store.VerifyCredentialAsync(
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenValid_ReturnsMappedAccount()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredPasswordHash)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, Password))
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();
        SetupMapping(userManager, user);

        var result = await store.VerifyCredentialAsync(
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(LocalAccountId, result.LocalAccountId);
        Assert.Equal(string.Empty, result.TenantId);
        Assert.Equal(UserName, result.UserName);
        Assert.True(result.IsEnabled);
        Assert.Equal(SecurityStamp, result.SecurityStamp);
        Assert.Equal(ConcurrencyToken, result.ConcurrencyToken);
        var claim = Assert.Single(result.Claims);
        Assert.Equal("role", claim.Type);
        Assert.Equal("admin", claim.Value);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheUserHasNoPassword_VerifiesAgainstTheDummyHash()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true).Verifiable();
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null)
            .Verifiable();
        passwordHasher
            .Setup(x =>
                x.VerifyHashedPassword(It.IsAny<IdentityUser>(), DummyPasswordHash, Password)
            )
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();

        var result = await store.VerifyCredentialAsync(
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheStoredHashIsEmpty_VerifiesAgainstTheDummyHash()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true).Verifiable();
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty)
            .Verifiable();
        passwordHasher
            .Setup(x =>
                x.VerifyHashedPassword(It.IsAny<IdentityUser>(), DummyPasswordHash, Password)
            )
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();

        var result = await store.VerifyCredentialAsync(
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheDecomposedFormIsSubmitted_VerifiesTheComposedForm()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredPasswordHash)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();
        SetupMapping(userManager, user);

        var result = await store.VerifyCredentialAsync(
            UserName,
            DecomposedPasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenOnlyTheSubmittedFormVerifies_RehashesTheComposedForm()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredPasswordHash)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, DecomposedPassword))
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();
        passwordHasher
            .Setup(x => x.HashPassword(user, ComposedPassword))
            .Returns(RehashedPasswordHash)
            .Verifiable();
        passwordStore
            .Setup(x =>
                x.SetPasswordHashAsync(user, RehashedPasswordHash, It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask)
            .Verifiable();
        userManager
            .Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        SetupMapping(userManager, user);

        var result = await store.VerifyCredentialAsync(
            UserName,
            DecomposedPasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenAWrongPasswordIsNotInNfc_ReturnsNull()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true).Verifiable();
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredPasswordHash)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, DecomposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();

        var result = await store.VerifyCredentialAsync(
            UserName,
            DecomposedPasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheHasherRequestsARehash_RehashesWithoutRotatingTheSecurityStamp()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        var user = CreateUser();
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        passwordStore
            .Setup(x => x.GetPasswordHashAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredPasswordHash)
            .Verifiable();
        passwordHasher
            .Setup(x => x.VerifyHashedPassword(user, StoredPasswordHash, Password))
            .Returns(PasswordVerificationResult.SuccessRehashNeeded)
            .Verifiable();
        passwordHasher
            .Setup(x => x.HashPassword(user, Password))
            .Returns(RehashedPasswordHash)
            .Verifiable();
        passwordStore
            .Setup(x =>
                x.SetPasswordHashAsync(user, RehashedPasswordHash, It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask)
            .Verifiable();
        userManager
            .Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        SetupMapping(userManager, user);

        var result = await store.VerifyCredentialAsync(
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenUserNotFoundAndTheDecomposedFormIsSubmitted_VerifiesBothFormsAgainstTheDummyHash()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        SetupDummyPasswordHash(passwordHasher);
        userManager
            .Setup(x => x.FindByNameAsync(UserName))
            .ReturnsAsync((IdentityUser?)null)
            .Verifiable();
        passwordHasher
            .Setup(x =>
                x.VerifyHashedPassword(
                    It.IsAny<IdentityUser>(),
                    DummyPasswordHash,
                    ComposedPassword
                )
            )
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();
        passwordHasher
            .Setup(x =>
                x.VerifyHashedPassword(
                    It.IsAny<IdentityUser>(),
                    DummyPasswordHash,
                    DecomposedPassword
                )
            )
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();

        var result = await store.VerifyCredentialAsync(
            UserName,
            DecomposedPasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region GetByIdOrDefaultAsync Tests

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenNotFound_ReturnsNull()
    {
        var (store, userManager) = CreateStore();
        userManager
            .Setup(x => x.FindByIdAsync(LocalAccountId))
            .ReturnsAsync((IdentityUser?)null)
            .Verifiable();

        var result = await store.GetByIdOrDefaultAsync(LocalAccountId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdOrDefaultAsync_WhenFound_ReturnsMappedAccount()
    {
        var (store, userManager) = CreateStore();
        var user = CreateUser();
        userManager.Setup(x => x.FindByIdAsync(LocalAccountId)).ReturnsAsync(user).Verifiable();
        SetupMapping(userManager, user);

        var result = await store.GetByIdOrDefaultAsync(LocalAccountId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LocalAccountId, result.LocalAccountId);
        Assert.True(result.IsEnabled);
        var claim = Assert.Single(result.Claims);
        Assert.Equal("admin", claim.Value);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WhenPasswordAndClaims_StoresTheHashAndCreatesUserWithIdAndClaims()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);

        passwordHasher
            .Setup(x => x.HashPassword(It.IsAny<IdentityUser>(), Password))
            .Returns(PasswordHash)
            .Verifiable();
        passwordStore
            .Setup(x =>
                x.SetPasswordHashAsync(
                    It.IsAny<IdentityUser>(),
                    PasswordHash,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable();
        IdentityUser? created = null;
        userManager
            .Setup(x => x.CreateAsync(It.IsAny<IdentityUser>()))
            .Callback<IdentityUser>(user => created = user)
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        userManager
            .Setup(x =>
                x.AddClaimAsync(
                    It.IsAny<IdentityUser>(),
                    It.Is<Claim>(claim => claim.Type == "role" && claim.Value == "admin")
                )
            )
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();

        await store.AddAsync(
            CreateAccount(claims: ("role", "admin")),
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(created);
        Assert.Equal(LocalAccountId, created.Id);
        Assert.Equal(UserName, created.UserName);
        Assert.Equal("alice@example.com", created.Email);
        Assert.True(created.EmailConfirmed);
    }

    [Fact]
    public async Task AddAsync_WhenDisabled_LocksOutUser()
    {
        var (store, userManager) = CreateStore();
        userManager
            .Setup(x => x.CreateAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true);
        userManager
            .Setup(x => x.SetLockoutEnabledAsync(It.IsAny<IdentityUser>(), true))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        userManager
            .Setup(x => x.SetLockoutEndDateAsync(It.IsAny<IdentityUser>(), DateTimeOffset.MaxValue))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();

        await store.AddAsync(
            CreateAccount(isEnabled: false),
            password: null,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task AddAsync_WhenCreateFails_Throws()
    {
        var (store, userManager) = CreateStore();
        userManager
            .Setup(x => x.CreateAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "boom" }))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.AddAsync(CreateAccount(), password: null, CancellationToken.None)
        );
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenMissing_Throws()
    {
        var (store, userManager) = CreateStore();
        userManager
            .Setup(x => x.FindByIdAsync(LocalAccountId))
            .ReturnsAsync((IdentityUser?)null)
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.UpdateAsync(CreateAccount(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenEnabled_UpdatesProfileAndEnables()
    {
        var (store, userManager) = CreateStore();
        var user = CreateUser();
        userManager.Setup(x => x.FindByIdAsync(LocalAccountId)).ReturnsAsync(user).Verifiable();
        userManager
            .Setup(x => x.SetUserNameAsync(user, UserName))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        userManager
            .Setup(x => x.SetEmailAsync(user, "alice@example.com"))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true);
        userManager
            .Setup(x => x.SetLockoutEndDateAsync(user, null))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        userManager
            .Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();

        await store.UpdateAsync(CreateAccount(), CancellationToken.None);

        // Confirmation is applied after SetEmailAsync (which resets it).
        Assert.True(user.EmailConfirmed);
    }

    #endregion

    #region SetPasswordAsync Tests

    [Fact]
    public async Task SetPasswordAsync_StoresTheHashAndRotatesTheSecurityStamp()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        var user = CreateUser();
        userManager.Setup(x => x.FindByIdAsync(LocalAccountId)).ReturnsAsync(user).Verifiable();
        passwordHasher
            .Setup(x => x.HashPassword(user, Password))
            .Returns(PasswordHash)
            .Verifiable();
        passwordStore
            .Setup(x => x.SetPasswordHashAsync(user, PasswordHash, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        userManager
            .Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();

        await store.SetPasswordAsync(LocalAccountId, PasswordBytes, CancellationToken.None);
    }

    [Fact]
    public async Task SetPasswordAsync_WhenTheDecomposedFormIsGiven_HashesTheComposedForm()
    {
        var passwordHasher = CreatePasswordHasher();
        var passwordStore = CreatePasswordStore();
        var (store, userManager) = CreateStore(passwordHasher.Object, passwordStore.Object);
        var user = CreateUser();
        userManager.Setup(x => x.FindByIdAsync(LocalAccountId)).ReturnsAsync(user).Verifiable();
        passwordHasher
            .Setup(x => x.HashPassword(user, ComposedPassword))
            .Returns(PasswordHash)
            .Verifiable();
        passwordStore
            .Setup(x => x.SetPasswordHashAsync(user, PasswordHash, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        userManager
            .Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();

        await store.SetPasswordAsync(
            LocalAccountId,
            DecomposedPasswordBytes,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task SetPasswordAsync_WhenTheUserStoreDoesNotStorePasswords_Throws()
    {
        var userStore = _mocks.Create<IUserStore<IdentityUser>>();
        var (store, _) = CreateStore(userStore: userStore.Object);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await store.SetPasswordAsync(LocalAccountId, PasswordBytes, CancellationToken.None)
        );
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenFound_DeletesUser()
    {
        var (store, userManager) = CreateStore();
        var user = CreateUser();
        userManager.Setup(x => x.FindByIdAsync(LocalAccountId)).ReturnsAsync(user).Verifiable();
        userManager
            .Setup(x => x.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();

        await store.RemoveAsync(LocalAccountId, CancellationToken.None);
    }

    [Fact]
    public async Task RemoveAsync_WhenNotFound_IsNoOp()
    {
        var (store, userManager) = CreateStore();
        userManager
            .Setup(x => x.FindByIdAsync(LocalAccountId))
            .ReturnsAsync((IdentityUser?)null)
            .Verifiable();

        await store.RemoveAsync(LocalAccountId, CancellationToken.None);
    }

    #endregion

    #region ReplaceClaimsAsync Tests

    [Fact]
    public async Task ReplaceClaimsAsync_RemovesExistingAndAddsNew()
    {
        var (store, userManager) = CreateStore();
        var user = CreateUser();
        var existing = new List<Claim> { new("old", "value") };
        userManager.Setup(x => x.FindByIdAsync(LocalAccountId)).ReturnsAsync(user).Verifiable();
        userManager.Setup(x => x.GetClaimsAsync(user)).ReturnsAsync(existing).Verifiable();
        userManager
            .Setup(x => x.RemoveClaimsAsync(user, existing))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();
        userManager
            .Setup(x =>
                x.AddClaimsAsync(
                    user,
                    It.Is<IEnumerable<Claim>>(claims => claims.Any(claim => claim.Type == "role"))
                )
            )
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable();

        await store.ReplaceClaimsAsync(
            LocalAccountId,
            [new PersistedLocalAccountClaim { Type = "role", Value = "admin" }],
            CancellationToken.None
        );
    }

    #endregion

    #region GetPageAsync Tests

    [Fact]
    public async Task GetPageAsync_WhenQueryableUnsupported_Throws()
    {
        var (store, userManager) = CreateStore();
        userManager.SetupGet(x => x.SupportsQueryableUsers).Returns(false).Verifiable();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await store.GetPageAsync(new PageRequest { Limit = 10 }, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetPageAsync_WhenSupported_ReturnsMappedPage()
    {
        var (store, userManager) = CreateStore();
        var users = new[]
        {
            new IdentityUser("alice") { Id = "user-1" },
            new IdentityUser("bob") { Id = "user-2" },
        };
        userManager.SetupGet(x => x.SupportsQueryableUsers).Returns(true).Verifiable();
        userManager.SetupGet(x => x.Users).Returns(users.AsQueryable()).Verifiable();
        userManager.Setup(x => x.GetClaimsAsync(It.IsAny<IdentityUser>())).ReturnsAsync([]);
        userManager.SetupGet(x => x.SupportsUserLockout).Returns(true);
        userManager.Setup(x => x.IsLockedOutAsync(It.IsAny<IdentityUser>())).ReturnsAsync(false);

        var page = await store.GetPageAsync(new PageRequest { Limit = 10 }, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
        Assert.Equal("user-1", page.Items[0].LocalAccountId);
        Assert.Equal("user-2", page.Items[1].LocalAccountId);
    }

    #endregion
}
