// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Setup.Contracts;
using NCode.Identity.OpenId.Setup.Logging;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides a default implementation of <see cref="IInstanceSetupService"/>. The root administrator is a local account
/// in the control-plane tenant that holds <see cref="BuiltInRoles.GlobalAdmin"/> at the platform node. The account, its
/// identity, the role assignment and the initialized mark are staged on one store manager and saved once, so the
/// instance is initialized exactly when its root administrator exists.
/// </summary>
internal sealed class DefaultInstanceSetupService(
    IStoreManagerFactory storeManagerFactory,
    ITenantResolver tenantResolver,
    IAmbientTenantAccessor ambientTenantAccessor,
    ILocalAccountCreator localAccountCreator,
    ICryptoService cryptoService,
    TimeProvider timeProvider,
    ILogger<DefaultInstanceSetupService> logger
) : IInstanceSetupService
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private ITenantResolver TenantResolver { get; } = tenantResolver;

    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    private ILocalAccountCreator LocalAccountCreator { get; } = localAccountCreator;

    private ICryptoService CryptoService { get; } = cryptoService;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultInstanceSetupService> Logger { get; } = logger;

    private static ManagementError CreateError(int statusCode, string detail) =>
        new() { StatusCode = statusCode, Detail = detail };

    private static InstanceSetupResult CreateAlreadySetUpResult() =>
        new()
        {
            Error = CreateError(StatusCodes.Status404NotFound, "The instance is already set up."),
        };

    /// <inheritdoc />
    public async ValueTask<bool> IsInitializedAsync(CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IInstanceSetupStore>();

        var setup = await store.GetOrDefaultAsync(cancellationToken);
        return setup?.IsInitialized ?? false;
    }

    /// <inheritdoc />
    public async ValueTask<InstanceSetupResult> CreateRootAsync(
        SetupRootOptions root,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(root);

        if (string.IsNullOrWhiteSpace(root.UserName))
        {
            return new InstanceSetupResult
            {
                Error = CreateError(
                    StatusCodes.Status400BadRequest,
                    "The root administrator's username is required."
                ),
            };
        }

        if (
            root.Password is not { } password
            || password.Length < SetupDefaults.MinimumRootPasswordLength
        )
        {
            return new InstanceSetupResult
            {
                Error = CreateError(
                    StatusCodes.Status400BadRequest,
                    $"The root administrator's password must be at least {SetupDefaults.MinimumRootPasswordLength} characters."
                ),
            };
        }

        var tenantId = TenantResolver.GetControlPlaneTenantId();
        using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var setup = await InstanceSetupRecords.GetOrAddAsync(
            StoreManagerFactory,
            cancellationToken
        );
        if (setup.IsInitialized)
            return CreateAlreadySetUpResult();

        var creation = await LocalAccountCreator.StageAsync(
            storeManager,
            tenantId,
            new CreateLocalAccountRequest
            {
                UserName = root.UserName,
                Password = password,
                Email = root.Email,
                EmailVerified = false,
                IsEnabled = true,
            },
            cancellationToken
        );
        if (!creation.Succeeded)
            return new InstanceSetupResult { Error = creation.Error };

        await storeManager
            .GetStore<IRoleAssignmentStore>()
            .AddAsync(
                new PersistedRoleAssignment
                {
                    TenantId = tenantId,
                    AssignmentId = CryptoService.GenerateResourceId(),
                    PrincipalId = creation.PrincipalId,
                    RoleName = BuiltInRoles.GlobalAdmin,
                    ResourceType = OpenIdResourceTypes.Platform,
                    ResourceId = OpenIdConstants.PlatformResourceId,
                    ConcurrencyToken = string.Empty,
                },
                cancellationToken
            );

        setup.IsInitialized = true;
        setup.InitializedAt = TimeProvider.GetUtcNow();
        setup.InitializedByPrincipalId = creation.PrincipalId;
        setup.TokenHash = null;
        setup.TokenIssuedAt = null;
        setup.TokenExpiresAt = null;

        try
        {
            await storeManager
                .GetStore<IInstanceSetupStore>()
                .UpdateAsync(setup, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another attempt marked the instance initialized between the read above and this write, so it won.
            return CreateAlreadySetUpResult();
        }

        Logger.RootAdministratorCreated(tenantId, creation.PrincipalId);

        return new InstanceSetupResult
        {
            Root = new CreateRootResponse
            {
                TenantId = tenantId,
                PrincipalId = creation.PrincipalId,
                UserName = root.UserName,
            },
        };
    }
}
