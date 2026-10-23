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
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Accounts;

public class LocalAccountInteractivePasswordChangerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string PrincipalId = "principal-1";
    private const string LocalAccountId = "account-1";
    private const string UserName = "user";
    private const string CurrentPassword = "current";
    private const string NewPassword = "new";
    private const string ExternalIssuer = "https://idp.example";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IFederatedIdentityStore> MockFederatedIdentityStore { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private LocalAccountInteractivePasswordChanger Changer { get; }

    public LocalAccountInteractivePasswordChangerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockFederatedIdentityStore = CreateStrictMock<IFederatedIdentityStore>();
        MockLocalAccountStore = CreateStrictMock<ILocalAccountStore>();
        Changer = new LocalAccountInteractivePasswordChanger(MockStoreManagerFactory.Object);
    }

    #region Scaffolding

    private static ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity("test"));

    private Mock<OpenIdContext> CreateContext()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.GetSubjectId(Principal)).Returns(PrincipalId).Verifiable();
        return mockContext;
    }

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(MockFederatedIdentityStore.Object)
            .Verifiable();
    }

    private void SetupIdentities(params PersistedFederatedIdentity[] identities) =>
        MockFederatedIdentityStore
            .Setup(x => x.GetByPrincipalAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(identities)
            .Verifiable();

    private void SetupAccount(PersistedLocalAccount? account)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(MockLocalAccountStore.Object)
            .Verifiable();
        MockLocalAccountStore
            .Setup(x => x.GetByIdOrDefaultAsync(LocalAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account)
            .Verifiable();
    }

    private void SetupVerification(PersistedLocalAccount? verified) =>
        MockLocalAccountStore
            .Setup(x =>
                x.VerifyCredentialAsync(
                    UserName,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(verified)
            .Verifiable();

    private void SetupPasswordChange()
    {
        MockLocalAccountStore
            .Setup(x =>
                x.SetPasswordAsync(
                    LocalAccountId,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private static PersistedFederatedIdentity SelfIssuedIdentity() =>
        new()
        {
            TenantId = TenantId,
            FederatedIdentityId = "fed-1",
            PrincipalId = PrincipalId,
            Issuer = AccountConstants.SelfIssuer,
            Subject = LocalAccountId,
            JoinKey = null,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedFederatedIdentity ExternalIdentity() =>
        new()
        {
            TenantId = TenantId,
            FederatedIdentityId = "fed-ext",
            PrincipalId = PrincipalId,
            Issuer = ExternalIssuer,
            Subject = "external-subject",
            JoinKey = null,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedLocalAccount Account(bool isEnabled) =>
        new()
        {
            TenantId = TenantId,
            LocalAccountId = LocalAccountId,
            UserName = UserName,
            Email = null,
            EmailVerified = false,
            IsEnabled = isEnabled,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

    #endregion

    #region ChangePasswordAsync Tests

    [Fact]
    public async Task ChangePasswordAsync_WhenTheCurrentPasswordIsValid_SetsTheNewPassword()
    {
        var mockContext = CreateContext();
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(Account(isEnabled: true));
        SetupVerification(Account(isEnabled: true));
        SetupPasswordChange();

        var result = await Changer.ChangePasswordAsync(
            mockContext.Object,
            Principal,
            CurrentPassword,
            NewPassword,
            CancellationToken.None
        );

        Assert.Equal(InteractivePasswordChangeResult.Changed, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenTheCurrentPasswordIsInvalid_ChangesNothing()
    {
        var mockContext = CreateContext();
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(Account(isEnabled: true));
        SetupVerification(verified: null);

        var result = await Changer.ChangePasswordAsync(
            mockContext.Object,
            Principal,
            CurrentPassword,
            NewPassword,
            CancellationToken.None
        );

        Assert.Equal(InteractivePasswordChangeResult.CurrentPasswordInvalid, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenThePrincipalHasNoLocalAccount_ReturnsNotSupported()
    {
        var mockContext = CreateContext();
        SetupStoreManager();
        SetupIdentities(ExternalIdentity());

        var result = await Changer.ChangePasswordAsync(
            mockContext.Object,
            Principal,
            CurrentPassword,
            NewPassword,
            CancellationToken.None
        );

        Assert.Equal(InteractivePasswordChangeResult.NotSupported, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenTheAccountIsDisabled_ReturnsNotSupported()
    {
        var mockContext = CreateContext();
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(Account(isEnabled: false));

        var result = await Changer.ChangePasswordAsync(
            mockContext.Object,
            Principal,
            CurrentPassword,
            NewPassword,
            CancellationToken.None
        );

        Assert.Equal(InteractivePasswordChangeResult.NotSupported, result);
    }

    #endregion
}
