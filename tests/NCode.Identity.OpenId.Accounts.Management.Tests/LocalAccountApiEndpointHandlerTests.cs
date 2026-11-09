// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq.Protected;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Endpoints;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Results;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Management;

public sealed class LocalAccountApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string TeamId = "team-1";
    private const string LocalAccountId = "account-1";
    private const string UserName = "alice";
    private const string Password = "a-long-unlisted-passphrase";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private Mock<IFederatedIdentityStore> MockFederatedIdentityStore { get; }
    private Mock<IFederatedPrincipalStore> MockFederatedPrincipalStore { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ILocalAccountValidator> MockLocalAccountValidator { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private Mock<IOpenIdContextFactory> MockOpenIdContextFactory { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IPasswordPolicy> MockPasswordPolicy { get; }
    private Mock<IPostCommitSecurityEffects> MockPostCommitSecurityEffects { get; }
    private LocalAccountApiEndpointHandler Handler { get; }
    private List<string> Calls { get; } = [];

    public LocalAccountApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockLocalAccountStore = MockRepository.Create<ILocalAccountStore>();
        MockFederatedIdentityStore = MockRepository.Create<IFederatedIdentityStore>();
        MockFederatedPrincipalStore = MockRepository.Create<IFederatedPrincipalStore>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockLocalAccountValidator = MockRepository.Create<ILocalAccountValidator>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
        MockSessionService = MockRepository.Create<ISessionService>();
        MockOpenIdContextFactory = MockRepository.Create<IOpenIdContextFactory>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockPasswordPolicy = MockRepository.Create<IPasswordPolicy>();
        MockPostCommitSecurityEffects = MockRepository.Create<IPostCommitSecurityEffects>();

        Handler = new LocalAccountApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockCryptoService.Object,
            MockLocalAccountValidator.Object,
            MockManagementAuditRecorder.Object,
            MockSessionService.Object,
            MockOpenIdContextFactory.Object,
            MockEffectiveSettingsResolver.Object,
            MockPasswordPolicy.Object,
            MockPostCommitSecurityEffects.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private void SetupAuthorizeCreate(ManagementError? error)
    {
        MockLocalAccountValidator
            .Setup(x =>
                x.AuthorizeCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();
    }

    private void SetupAuthorizeUpdate(ManagementError? error)
    {
        MockLocalAccountValidator
            .Setup(x =>
                x.AuthorizeUpdateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();
    }

    private void SetupAuthorizeDelete(ManagementError? error)
    {
        MockLocalAccountValidator
            .Setup(x =>
                x.AuthorizeDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();
    }

    private static ManagementError CreateForbiddenError() =>
        new()
        {
            StatusCode = StatusCodes.Status403Forbidden,
            Detail = "The caller is not authorized.",
        };

    // Mocks the store manager and reads the account; the store manager is only created once authorization succeeded.
    private void SetupAccount(PersistedLocalAccount? account)
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
        MockLocalAccountStore
            .Setup(x => x.GetByIdOrDefaultAsync(LocalAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account)
            .Verifiable();
    }

    private void SetupDeleteValidated()
    {
        MockLocalAccountValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    LocalAccountId,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
    }

    // The password is judged by the tenant's effective settings, read from its stored document.
    private void SetupPasswordAccepted()
    {
        var tenant = CreateTenant();
        var teamSettings = Mock.Of<IReadOnlySettingCollection>();
        var tenantSettings = Mock.Of<IReadOnlySettingCollection>();

        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.Is<JsonElement>(element => element.GetRawText() == "{}")
                )
            )
            .Returns(tenantSettings)
            .Verifiable();
        MockPasswordPolicy
            .Setup(x =>
                x.ValidateAsync(
                    It.Is<PasswordPolicyContext>(context =>
                        ReferenceEquals(context.Settings, tenantSettings)
                        && context.UserName == UserName
                    ),
                    It.IsAny<ReadOnlyMemory<char>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(PasswordPolicyResult.Accepted)
            .Verifiable();
    }

    private void SetupTenantIsNotServed()
    {
        MockOpenIdContextFactory
            .Setup(x =>
                x.CreateForTenantAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    It.IsAny<IMediator>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(TypedResults.NotFound().AsException("The tenant is not served."))
            .Verifiable();
    }

    private void SetupTenantContext()
    {
        var openIdContext = MockRepository.Create<OpenIdContext>();
        openIdContext
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockOpenIdContextFactory
            .Setup(x =>
                x.CreateForTenantAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    It.IsAny<IMediator>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(openIdContext.Object)
            .Verifiable();
    }

    private void SetupSelfIssuedIdentity()
    {
        var identity = new PersistedFederatedIdentity
        {
            TenantId = TenantId,
            FederatedIdentityId = "identity-1",
            PrincipalId = "principal-1",
            Issuer = AccountConstants.SelfIssuer,
            Subject = LocalAccountId,
            JoinKey = null,
            ConcurrencyToken = "identity-ct",
        };

        MockStoreManager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(MockFederatedIdentityStore.Object)
            .Verifiable();
        MockFederatedIdentityStore
            .Setup(x =>
                x.GetByIssuerSubjectAsync(
                    AccountConstants.SelfIssuer,
                    LocalAccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(identity)
            .Verifiable();
    }

    private void SetupNoSelfIssuedIdentity()
    {
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(MockFederatedIdentityStore.Object)
            .Verifiable();
        MockFederatedIdentityStore
            .Setup(x =>
                x.GetByIssuerSubjectAsync(
                    AccountConstants.SelfIssuer,
                    LocalAccountId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PersistedFederatedIdentity?)null)
            .Verifiable();
    }

    private void SetupAuditRecorded(string resourceId = LocalAccountId)
    {
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.Is<ResourceChangeAuditEvent>(e => e.ResourceId == resourceId)
                )
            )
            .Callback(() => Calls.Add("audit"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private static PersistedLocalAccount CreateAccount() =>
        new()
        {
            TenantId = TenantId,
            LocalAccountId = LocalAccountId,
            UserName = UserName,
            Email = null,
            EmailVerified = false,
            IsEnabled = true,
            Claims = [],
            ConcurrencyToken = "account-ct",
        };

    private static PersistedTenant CreateTenant() =>
        new PersistedTenantBuilder
        {
            TenantId = TenantId,
            TeamId = TeamId,
            Issuer = "https://op.example/tenant-1",
        }.Build();

    private static HttpContext CreateHttpContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IMediator>());
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            RequestServices = services.BuildServiceProvider(),
        };
    }

    #endregion

    #region SetEnabledAsync Tests

    [Fact]
    public async Task SetEnabledAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheAccount()
    {
        SetupAuthorizeUpdate(CreateForbiddenError());

        var result = await Handler.DisableLocalAccountAsync(
            CreateHttpContext(),
            TenantId,
            LocalAccountId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task SetEnabledAsync_WhenDisablingAndTheTenantIsNotServed_RefusesBeforeWriting()
    {
        SetupAuthorizeUpdate(error: null);
        SetupAccount(CreateAccount());
        SetupSelfIssuedIdentity();
        SetupTenantIsNotServed();
        var httpContext = CreateHttpContext();

        // No UpdateAsync/SaveChangesAsync, session or audit scaffold: a strict-mock failure would mean the account was
        // disabled, its sessions ended or the change audited although the tenant is not served.
        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await Handler.DisableLocalAccountAsync(
                httpContext,
                TenantId,
                LocalAccountId,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task SetEnabledAsync_WhenEnabling_DoesNotMaterializeTheTenant()
    {
        var account = CreateAccount();
        account.IsEnabled = false;
        SetupAuthorizeUpdate(error: null);
        SetupAccount(account);
        MockLocalAccountStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedLocalAccount>(updated => updated.IsEnabled),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.Is<ResourceChangeAuditEvent>(e => e.ResourceId == LocalAccountId)
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var httpContext = CreateHttpContext();

        // No CreateForTenantAsync or session scaffold: a strict-mock failure would mean enabling an account
        // materialized the tenant or ended sessions.
        var result = await Handler.EnableLocalAccountAsync(
            httpContext,
            TenantId,
            LocalAccountId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.True(account.IsEnabled);
    }

    [Fact]
    public async Task SetEnabledAsync_WhenDisabling_EndsTheSessionsBeforeTheAudit()
    {
        var account = CreateAccount();
        SetupAuthorizeUpdate(error: null);
        SetupAccount(account);
        SetupTenantContext();
        SetupSelfIssuedIdentity();
        MockLocalAccountStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedLocalAccount>(updated => !updated.IsEnabled),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockPostCommitSecurityEffects
            .Setup(x =>
                x.TerminateSessionsAsync(
                    It.IsAny<OpenIdContext>(),
                    "principal-1",
                    SessionTerminationReasons.AccountDisabled
                )
            )
            .Callback(() => Calls.Add("sessions"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAuditRecorded();
        var httpContext = CreateHttpContext();

        var result = await Handler.DisableLocalAccountAsync(
            httpContext,
            TenantId,
            LocalAccountId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal(["sessions", "audit"], Calls);
    }

    [Fact]
    public async Task SetEnabledAsync_WhenDisablingAnAccountWithoutASelfIssuedIdentity_RecordsTheAuditWithoutEndingSessions()
    {
        var account = CreateAccount();
        SetupAuthorizeUpdate(error: null);
        SetupAccount(account);
        SetupNoSelfIssuedIdentity();
        MockLocalAccountStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedLocalAccount>(updated => !updated.IsEnabled),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAuditRecorded();
        var httpContext = CreateHttpContext();

        var result = await Handler.DisableLocalAccountAsync(
            httpContext,
            TenantId,
            LocalAccountId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal(["audit"], Calls);
    }

    #endregion

    #region ResetPasswordAsync Tests

    [Fact]
    public async Task ResetPasswordAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheAccount()
    {
        SetupAuthorizeUpdate(CreateForbiddenError());

        var result = await Handler.ResetPasswordAsync(
            CreateHttpContext(),
            TenantId,
            LocalAccountId,
            new ResetLocalAccountPasswordRequest { Password = Password },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTheTenantIsNotServed_RefusesBeforeWriting()
    {
        SetupAuthorizeUpdate(error: null);
        SetupAccount(CreateAccount());
        SetupPasswordAccepted();
        SetupSelfIssuedIdentity();
        SetupTenantIsNotServed();
        var httpContext = CreateHttpContext();

        // No SetPasswordAsync/SaveChangesAsync, session, audit or attempt-limiter scaffold: a strict-mock failure would
        // mean the password was written, its sessions ended or the change audited although the tenant is not served.
        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await Handler.ResetPasswordAsync(
                httpContext,
                TenantId,
                LocalAccountId,
                new ResetLocalAccountPasswordRequest { Password = Password },
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenAccepted_EndsTheSessionsAndClearsTheCountBeforeTheAudit()
    {
        SetupAuthorizeUpdate(error: null);
        SetupAccount(CreateAccount());
        SetupPasswordAccepted();
        SetupTenantContext();
        SetupSelfIssuedIdentity();
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
        MockPostCommitSecurityEffects
            .Setup(x =>
                x.TerminateSessionsAsync(
                    It.IsAny<OpenIdContext>(),
                    "principal-1",
                    SessionTerminationReasons.PasswordReset
                )
            )
            .Callback(() => Calls.Add("sessions"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockPostCommitSecurityEffects
            .Setup(x => x.ResetPasswordAttemptsAsync(TenantId, UserName))
            .Callback(() => Calls.Add("attempts"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAuditRecorded();
        var httpContext = CreateHttpContext();

        var result = await Handler.ResetPasswordAsync(
            httpContext,
            TenantId,
            LocalAccountId,
            new ResetLocalAccountPasswordRequest { Password = Password },
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal(["sessions", "attempts", "audit"], Calls);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTheAccountHasNoSelfIssuedIdentity_ClearsTheCountAndRecordsTheAudit()
    {
        SetupAuthorizeUpdate(error: null);
        SetupAccount(CreateAccount());
        SetupPasswordAccepted();
        SetupNoSelfIssuedIdentity();
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
        MockPostCommitSecurityEffects
            .Setup(x => x.ResetPasswordAttemptsAsync(TenantId, UserName))
            .Callback(() => Calls.Add("attempts"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAuditRecorded();
        var httpContext = CreateHttpContext();

        var result = await Handler.ResetPasswordAsync(
            httpContext,
            TenantId,
            LocalAccountId,
            new ResetLocalAccountPasswordRequest { Password = Password },
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal(["attempts", "audit"], Calls);
    }

    #endregion

    #region SetClaimsAsync Tests

    [Fact]
    public async Task SetClaimsAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheAccount()
    {
        SetupAuthorizeUpdate(CreateForbiddenError());

        var result = await Handler.SetClaimsAsync(
            CreateHttpContext(),
            TenantId,
            LocalAccountId,
            new SetLocalAccountClaimsRequest { Claims = [] },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region SetMetadataAsync Tests

    [Fact]
    public async Task SetMetadataAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheAccount()
    {
        SetupAuthorizeUpdate(CreateForbiddenError());

        var result = await Handler.SetMetadataAsync(
            CreateHttpContext(),
            TenantId,
            LocalAccountId,
            new SetLocalAccountMetadataRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region CreateLocalAccountAsync Tests

    [Fact]
    public async Task CreateLocalAccountAsync_WhenUnauthorized_ReturnsTheErrorWithoutOpeningTheStore()
    {
        SetupAuthorizeCreate(CreateForbiddenError());

        var result = await Handler.CreateLocalAccountAsync(
            CreateHttpContext(),
            TenantId,
            new CreateLocalAccountRequest { UserName = UserName, Password = Password },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateLocalAccountAsync_WhenAccepted_ClearsTheCountBeforeTheAudit()
    {
        SetupAuthorizeCreate(error: null);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockLocalAccountValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    UserName,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("id-1")
            .Verifiable();
        SetupPasswordAccepted();
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(MockFederatedPrincipalStore.Object)
            .Verifiable();
        MockFederatedPrincipalStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedPrincipal>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(MockFederatedIdentityStore.Object)
            .Verifiable();
        MockFederatedIdentityStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedIdentity>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(MockLocalAccountStore.Object)
            .Verifiable();
        MockLocalAccountStore
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<PersistedLocalAccount>(),
                    It.IsAny<ReadOnlyMemory<byte>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockPostCommitSecurityEffects
            .Setup(x => x.ResetPasswordAttemptsAsync(TenantId, UserName))
            .Callback(() => Calls.Add("attempts"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupAuditRecorded("id-1");
        var httpContext = CreateHttpContext();

        var result = await Handler.CreateLocalAccountAsync(
            httpContext,
            TenantId,
            new CreateLocalAccountRequest { UserName = UserName, Password = Password },
            CancellationToken.None
        );

        Assert.IsType<Created<LocalAccountResource>>(result);
        Assert.Equal(["attempts", "audit"], Calls);
    }

    #endregion

    #region UpdateLocalAccountAsync Tests

    [Fact]
    public async Task UpdateLocalAccountAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheAccount()
    {
        SetupAuthorizeUpdate(CreateForbiddenError());

        var result = await Handler.UpdateLocalAccountAsync(
            CreateHttpContext(),
            TenantId,
            LocalAccountId,
            new UpdateLocalAccountRequest { UserName = UserName },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region DeleteLocalAccountAsync Tests

    [Fact]
    public async Task DeleteLocalAccountAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheAccount()
    {
        SetupAuthorizeDelete(CreateForbiddenError());

        var result = await Handler.DeleteLocalAccountAsync(
            CreateHttpContext(),
            TenantId,
            LocalAccountId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteLocalAccountAsync_WhenTheTenantIsNotServed_RefusesBeforeWriting()
    {
        SetupAuthorizeDelete(error: null);
        SetupAccount(CreateAccount());
        SetupDeleteValidated();
        SetupTenantIsNotServed();
        var httpContext = CreateHttpContext();

        // No identity lookup, RemoveAsync/SaveChangesAsync, session or audit scaffold: a strict-mock failure would mean
        // the account was deleted, its sessions ended or the change audited although the tenant is not served, whether
        // or not the account has a self-issued identity.
        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await Handler.DeleteLocalAccountAsync(
                httpContext,
                TenantId,
                LocalAccountId,
                CancellationToken.None
            )
        );
    }

    #endregion

    #region Map Tests

    [Theory]
    [InlineData("POST", "/local-accounts", new[] { 400, 401, 403, 404, 409, 412, 415 })]
    [InlineData("GET", "/local-accounts", new[] { 400, 401, 403 })]
    [InlineData("GET", "/local-accounts/{localAccountId}", new[] { 401, 403, 404 })]
    [InlineData(
        "PATCH",
        "/local-accounts/{localAccountId}",
        new[] { 400, 401, 403, 404, 409, 412, 415 }
    )]
    [InlineData("DELETE", "/local-accounts/{localAccountId}", new[] { 401, 403, 404, 412 })]
    [InlineData("POST", "/local-accounts/{localAccountId}/enable", new[] { 401, 403, 404, 412 })]
    [InlineData("POST", "/local-accounts/{localAccountId}/disable", new[] { 401, 403, 404, 412 })]
    [InlineData(
        "POST",
        "/local-accounts/{localAccountId}/password",
        new[] { 400, 401, 403, 404, 412, 415 }
    )]
    [InlineData("GET", "/local-accounts/{localAccountId}/claims", new[] { 401, 403, 404 })]
    [InlineData(
        "PUT",
        "/local-accounts/{localAccountId}/claims",
        new[] { 400, 401, 403, 404, 412, 415 }
    )]
    [InlineData("GET", "/local-accounts/{localAccountId}/metadata", new[] { 401, 403, 404 })]
    [InlineData(
        "PATCH",
        "/local-accounts/{localAccountId}/metadata",
        new[] { 400, 401, 403, 404, 412, 415 }
    )]
    public async Task Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers(
        string httpMethod,
        string routePattern,
        int[] statusCodes
    )
    {
        Assert.Equal(
            statusCodes,
            await EndpointProblemMetadata.ReadStatusCodesAsync(Handler, httpMethod, routePattern)
        );
        Assert.Empty(
            await EndpointProblemMetadata.ReadUndescribedStatusCodesAsync(
                Handler,
                httpMethod,
                routePattern
            )
        );
    }

    #endregion
}
