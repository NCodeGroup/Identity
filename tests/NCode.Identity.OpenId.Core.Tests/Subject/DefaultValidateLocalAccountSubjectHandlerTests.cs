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
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultValidateLocalAccountSubjectHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string PrincipalId = "principal-1";
    private const string LocalAccountId = "account-1";
    private const string IssuerClaimType = "iss";
    private const string ExternalIssuer = "https://idp.example";
    private const string AccountGoneMessage = "The end-user's account no longer exists.";
    private const string AccountDisabledOrGoneMessage =
        "The end-user's account is disabled or no longer exists.";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IFederatedIdentityStore> MockFederatedIdentityStore { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private DefaultValidateLocalAccountSubjectHandler Handler { get; }

    public DefaultValidateLocalAccountSubjectHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockFederatedIdentityStore = CreateStrictMock<IFederatedIdentityStore>();
        MockLocalAccountStore = CreateStrictMock<ILocalAccountStore>();
        Handler = new DefaultValidateLocalAccountSubjectHandler(MockStoreManagerFactory.Object);
    }

    #region Scaffolding

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

    private void SetupAccountStore() =>
        MockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(MockLocalAccountStore.Object)
            .Verifiable();

    private void SetupAccount(PersistedLocalAccount? account)
    {
        SetupAccountStore();
        MockLocalAccountStore
            .Setup(x => x.GetByIdOrDefaultAsync(LocalAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account)
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
            UserName = "user",
            Email = null,
            EmailVerified = false,
            IsEnabled = isEnabled,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

    // The tenant's settings are read only for a principal that has no identity.
    private void SetupIssuerClaimType(Mock<OpenIdContext> mockContext)
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim))
            .Returns(IssuerClaimType)
            .Verifiable();

        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object).Verifiable();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object).Verifiable();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    // The error factory is read only when the handler refuses the subject.
    private void SetupAccessDenied(Mock<OpenIdContext> mockContext, string description)
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupSet(x => x.Description = description).Verifiable();

        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(OpenIdProtocol.ErrorCodes.AccessDenied))
            .Returns(mockError.Object)
            .Verifiable();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    private ValidateSubjectStandingCommand CreateCommand(
        Mock<OpenIdContext> mockContext,
        OperationDisposition<IOpenIdError> disposition,
        string? issuer = null
    )
    {
        var identity = new ClaimsIdentity("test");
        if (issuer is not null)
        {
            identity.AddClaim(new Claim(IssuerClaimType, issuer));
        }

        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            new ClaimsPrincipal(identity),
            PrincipalId
        );

        return new ValidateSubjectStandingCommand(
            mockContext.Object,
            subjectAuthentication,
            disposition
        );
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAlreadyHasError_ShortCircuits()
    {
        var mockInitialError = CreateLooseMock<IOpenIdError>();
        var disposition = new OperationDisposition<IOpenIdError>
        {
            Error = mockInitialError.Object,
        };
        var command = CreateCommand(CreateStrictMock<OpenIdContext>(), disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Same(mockInitialError.Object, disposition.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenTheLocalAccountIsEnabled_LeavesNoError()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(Account(isEnabled: true));
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(CreateStrictMock<OpenIdContext>(), disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenTheLocalAccountIsDisabled_SetsAccessDenied()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(Account(isEnabled: false));
        var mockContext = CreateStrictMock<OpenIdContext>();
        SetupAccessDenied(mockContext, AccountDisabledOrGoneMessage);
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(mockContext, disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenTheLocalAccountIsGone_SetsAccessDenied()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(account: null);
        var mockContext = CreateStrictMock<OpenIdContext>();
        SetupAccessDenied(mockContext, AccountDisabledOrGoneMessage);
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(mockContext, disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenALocalSubjectHasNoIdentity_SetsAccessDenied()
    {
        SetupStoreManager();
        SetupIdentities();
        var mockContext = CreateStrictMock<OpenIdContext>();
        SetupIssuerClaimType(mockContext);
        SetupAccessDenied(mockContext, AccountGoneMessage);
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(mockContext, disposition, AccountConstants.SelfIssuer);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenAnExternalSubjectHasNoIdentity_LeavesNoError()
    {
        SetupStoreManager();
        SetupIdentities();
        var mockContext = CreateStrictMock<OpenIdContext>();
        SetupIssuerClaimType(mockContext);
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(mockContext, disposition, ExternalIssuer);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenThePrincipalHasOnlyExternalIdentities_LeavesNoError()
    {
        SetupStoreManager();
        SetupIdentities(ExternalIdentity());
        SetupAccountStore();
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(CreateStrictMock<OpenIdContext>(), disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    #endregion
}
