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
using Moq;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Accounts;

public class LocalAccountInteractiveCredentialVerifierTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string LocalAccountId = "account-1";
    private const string UserName = "user";
    private const string Password = "password";

    private static DateTimeOffset Now { get; } = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private Mock<IAuthenticationContextReferenceResolver> MockResolver { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IPasswordAttemptLimiter> MockPasswordAttemptLimiter { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private LocalAccountInteractiveCredentialVerifier Verifier { get; }

    public LocalAccountInteractiveCredentialVerifierTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockLocalAccountStore = CreateStrictMock<ILocalAccountStore>();
        MockResolver = CreateStrictMock<IAuthenticationContextReferenceResolver>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockPasswordAttemptLimiter = CreateStrictMock<IPasswordAttemptLimiter>();
        MockContext = CreateStrictMock<OpenIdContext>();
        Verifier = new LocalAccountInteractiveCredentialVerifier(
            MockStoreManagerFactory.Object,
            MockResolver.Object,
            MockTimeProvider.Object,
            MockPasswordAttemptLimiter.Object
        );
    }

    #region Scaffolding

    private void SetupLimit(bool isReached) =>
        MockPasswordAttemptLimiter
            .Setup(x =>
                x.IsLimitReachedAsync(MockContext.Object, UserName, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(isReached)
            .Verifiable();

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(MockLocalAccountStore.Object)
            .Verifiable();
    }

    private void SetupPrincipalBuild()
    {
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.SubjectClaimTypes))
            .Returns((IReadOnlyCollection<string>)["sub"])
            .Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim))
            .Returns("iss")
            .Verifiable();

        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object).Verifiable();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object).Verifiable();
        MockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockResolver
            .Setup(x =>
                x.ResolveAsync(
                    It.IsAny<AuthenticationContextReferenceRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((string?)null)
            .Verifiable();
    }

    private static PersistedLocalAccount Account() =>
        new()
        {
            TenantId = TenantId,
            LocalAccountId = LocalAccountId,
            UserName = UserName,
            Email = null,
            EmailVerified = false,
            IsEnabled = true,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

    #endregion

    #region VerifyCredentialAsync Tests

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheAttemptLimitIsReached_RecordsAFailureAndReturnsNull()
    {
        SetupStoreManager();
        SetupLimit(isReached: true);
        LocalAccountStoreSetup.SetupVerification(
            MockLocalAccountStore,
            UserName,
            UserName,
            Account()
        );
        MockPasswordAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, UserName, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var principal = await Verifier.VerifyCredentialAsync(
            MockContext.Object,
            UserName,
            Password,
            CancellationToken.None
        );

        Assert.Null(principal);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheStoreVerifiesARefusedPassword_RecordsAFailureAndReturnsNull()
    {
        SetupStoreManager();
        SetupLimit(isReached: true);
        LocalAccountStoreSetup.SetupVerificationIgnoringARefusal(
            MockLocalAccountStore,
            UserName,
            UserName,
            Account()
        );
        MockPasswordAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, UserName, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var principal = await Verifier.VerifyCredentialAsync(
            MockContext.Object,
            UserName,
            Password,
            CancellationToken.None
        );

        Assert.Null(principal);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheStoreDoesNotAskTheGate_Throws()
    {
        SetupStoreManager();
        LocalAccountStoreSetup.SetupVerificationWithoutTheGate(
            MockLocalAccountStore,
            UserName,
            UserName,
            Account()
        );

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Verifier.VerifyCredentialAsync(
                MockContext.Object,
                UserName,
                Password,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenTheStoreMatchesAnotherSpelling_CountsTheStoredUserName()
    {
        SetupStoreManager();
        SetupLimit(isReached: true);
        LocalAccountStoreSetup.SetupVerification(
            MockLocalAccountStore,
            UserName + " ",
            UserName,
            Account()
        );
        MockPasswordAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, UserName, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var principal = await Verifier.VerifyCredentialAsync(
            MockContext.Object,
            UserName + " ",
            Password,
            CancellationToken.None
        );

        Assert.Null(principal);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenThePasswordIsWrong_RecordsAFailedAttempt()
    {
        using var cancellationSource = new CancellationTokenSource();
        SetupStoreManager();
        SetupLimit(isReached: false);
        LocalAccountStoreSetup.SetupVerification(
            MockLocalAccountStore,
            UserName,
            UserName,
            verified: null
        );
        MockPasswordAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, UserName, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var principal = await Verifier.VerifyCredentialAsync(
            MockContext.Object,
            UserName,
            Password,
            cancellationSource.Token
        );

        Assert.Null(principal);
    }

    [Fact]
    public async Task VerifyCredentialAsync_WhenThePasswordIsRight_ClearsTheFailedAttempts()
    {
        SetupStoreManager();
        SetupLimit(isReached: false);
        LocalAccountStoreSetup.SetupVerification(
            MockLocalAccountStore,
            UserName,
            UserName,
            Account()
        );
        SetupPrincipalBuild();
        MockPasswordAttemptLimiter
            .Setup(x => x.ResetAsync(TenantId, UserName, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var principal = await Verifier.VerifyCredentialAsync(
            MockContext.Object,
            UserName,
            Password,
            CancellationToken.None
        );

        Assert.NotNull(principal);
        Assert.Equal(LocalAccountId, principal.FindFirstValue("sub"));
    }

    #endregion
}
