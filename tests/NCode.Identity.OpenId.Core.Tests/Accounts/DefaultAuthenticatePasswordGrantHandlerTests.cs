// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Accounts;

public class DefaultAuthenticatePasswordGrantHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string LocalAccountId = "account-1";
    private const string UserName = "user";
    private const string Password = "password";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private Mock<IPrincipalResolver> MockPrincipalResolver { get; }
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private Mock<IPasswordAttemptLimiter> MockPasswordAttemptLimiter { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private DefaultAuthenticatePasswordGrantHandler Handler { get; }

    public DefaultAuthenticatePasswordGrantHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockLocalAccountStore = CreateStrictMock<ILocalAccountStore>();
        MockPrincipalResolver = CreateStrictMock<IPrincipalResolver>();
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        MockPasswordAttemptLimiter = CreateStrictMock<IPasswordAttemptLimiter>();
        MockContext = CreateStrictMock<OpenIdContext>();
        Handler = new DefaultAuthenticatePasswordGrantHandler(
            MockStoreManagerFactory.Object,
            MockPrincipalResolver.Object,
            MockAuditEventRecorder.Object,
            MockPasswordAttemptLimiter.Object
        );
    }

    #region Scaffolding

    private AuthenticatePasswordGrantCommand CreateCommand(string userName = UserName)
    {
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        mockTokenRequest.Setup(x => x.Username).Returns(userName).Verifiable();
        mockTokenRequest.Setup(x => x.Password).Returns(Password).Verifiable();

        return new AuthenticatePasswordGrantCommand(
            MockContext.Object,
            CreateStrictMock<OpenIdClient>().Object,
            mockTokenRequest.Object,
            new AuthenticationProperties()
        );
    }

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

    private void SetupSaveChanges() =>
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupTenant()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        MockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    private void SetupAudit(string errorCode, string errorMessage, string userName = UserName) =>
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    MockContext.Object,
                    errorCode,
                    errorMessage,
                    userName,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

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

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTheAttemptLimitIsReached_RecordsAFailureAndAuditsTheLimit()
    {
        var command = CreateCommand();
        SetupStoreManager();
        SetupLimit(isReached: true);
        LocalAccountStoreSetup.SetupVerification(
            MockLocalAccountStore,
            UserName,
            UserName,
            Account(isEnabled: true)
        );
        MockPasswordAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, UserName, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAudit(
            DefaultAuthenticatePasswordGrantHandler.AttemptLimitReachedErrorCode,
            DefaultAuthenticatePasswordGrantHandler.AttemptLimitReachedReason
        );

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.IsAuthenticated);
    }

    [Fact]
    public async Task HandleAsync_WhenTheStoreVerifiesARefusedPassword_RecordsAFailureAndAuditsTheLimit()
    {
        var command = CreateCommand();
        SetupStoreManager();
        SetupLimit(isReached: true);
        LocalAccountStoreSetup.SetupVerificationIgnoringARefusal(
            MockLocalAccountStore,
            UserName,
            UserName,
            Account(isEnabled: true)
        );
        MockPasswordAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, UserName, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAudit(
            DefaultAuthenticatePasswordGrantHandler.AttemptLimitReachedErrorCode,
            DefaultAuthenticatePasswordGrantHandler.AttemptLimitReachedReason
        );

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.IsAuthenticated);
    }

    [Fact]
    public async Task HandleAsync_WhenTheStoreMatchesAnotherSpelling_CountsTheStoredUserName()
    {
        var command = CreateCommand(UserName + " ");
        SetupStoreManager();
        SetupLimit(isReached: true);
        LocalAccountStoreSetup.SetupVerification(
            MockLocalAccountStore,
            UserName + " ",
            UserName,
            Account(isEnabled: true)
        );
        MockPasswordAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, UserName, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAudit(
            DefaultAuthenticatePasswordGrantHandler.AttemptLimitReachedErrorCode,
            DefaultAuthenticatePasswordGrantHandler.AttemptLimitReachedReason,
            UserName + " "
        );

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.IsAuthenticated);
    }

    [Fact]
    public async Task HandleAsync_WhenThePasswordIsWrong_RecordsAFailedAttempt()
    {
        using var cancellationSource = new CancellationTokenSource();
        var command = CreateCommand();
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
        SetupAudit(
            DefaultAuthenticatePasswordGrantHandler.InvalidCredentialsErrorCode,
            DefaultAuthenticatePasswordGrantHandler.InvalidCredentialsReason
        );

        var disposition = await Handler.HandleAsync(command, cancellationSource.Token);

        Assert.False(disposition.IsAuthenticated);
    }

    [Fact]
    public async Task HandleAsync_WhenThePasswordIsRightButTheAccountIsDisabled_ClearsTheFailedAttemptsAndFails()
    {
        var command = CreateCommand();
        SetupStoreManager();
        SetupLimit(isReached: false);
        LocalAccountStoreSetup.SetupVerification(
            MockLocalAccountStore,
            UserName,
            UserName,
            Account(isEnabled: false)
        );
        SetupSaveChanges();
        SetupTenant();
        MockPasswordAttemptLimiter
            .Setup(x => x.ResetAsync(TenantId, UserName, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAudit(
            DefaultAuthenticatePasswordGrantHandler.InvalidCredentialsErrorCode,
            DefaultAuthenticatePasswordGrantHandler.InvalidCredentialsReason
        );

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.IsAuthenticated);
    }

    #endregion
}
