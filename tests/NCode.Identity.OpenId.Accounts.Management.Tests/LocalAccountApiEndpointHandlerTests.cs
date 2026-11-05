// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Auditing;
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
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ILocalAccountValidator> MockLocalAccountValidator { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private Mock<IOpenIdContextFactory> MockOpenIdContextFactory { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IPasswordPolicy> MockPasswordPolicy { get; }
    private Mock<IPasswordAttemptLimiter> MockPasswordAttemptLimiter { get; }
    private LocalAccountApiEndpointHandler Handler { get; }

    public LocalAccountApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockLocalAccountStore = MockRepository.Create<ILocalAccountStore>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockLocalAccountValidator = MockRepository.Create<ILocalAccountValidator>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
        MockSessionService = MockRepository.Create<ISessionService>();
        MockOpenIdContextFactory = MockRepository.Create<IOpenIdContextFactory>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockPasswordPolicy = MockRepository.Create<IPasswordPolicy>();
        MockPasswordAttemptLimiter = MockRepository.Create<IPasswordAttemptLimiter>();

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
            MockPasswordAttemptLimiter.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private void SetupAuthorizationSucceeds()
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();
    }

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
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
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
        new()
        {
            TenantId = TenantId,
            TeamId = TeamId,
            Issuer = "https://op.example/tenant-1",
            ConcurrencyToken = "tenant-ct",
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                ConcurrencyToken = "settings-ct",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = "secrets-ct",
                Value = [],
            },
        };

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
    public async Task SetEnabledAsync_WhenDisablingAndTheTenantIsNotServed_RefusesBeforeWriting()
    {
        SetupAuthorizationSucceeds();
        SetupAccount(CreateAccount());
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
        SetupAuthorizationSucceeds();
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
                    It.Is<ResourceChangeAuditEvent>(e => e.ResourceId == LocalAccountId),
                    It.IsAny<CancellationToken>()
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

    #endregion

    #region ResetPasswordAsync Tests

    [Fact]
    public async Task ResetPasswordAsync_WhenTheTenantIsNotServed_RefusesBeforeWriting()
    {
        SetupAuthorizationSucceeds();
        SetupAccount(CreateAccount());
        SetupPasswordAccepted();
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

    #endregion

    #region DeleteLocalAccountAsync Tests

    [Fact]
    public async Task DeleteLocalAccountAsync_WhenTheTenantIsNotServed_RefusesBeforeWriting()
    {
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
}
