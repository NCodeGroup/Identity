// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Setup.Contracts;
using NCode.Identity.OpenId.Setup.Logging;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides a default implementation of <see cref="IInstanceSetupService"/>. The root administrator is a local account
/// in the control-plane tenant that holds <see cref="BuiltInRoles.GlobalAdmin"/> at the platform node. The account, its
/// identity, the role assignment, the management console's client when the request carries one, and the initialized
/// mark are staged on one store manager and saved once, so the instance is initialized exactly when its root
/// administrator exists. The console's client is staged through <see cref="IClientCreator"/>, so it passes the same
/// validation as a client that the management API creates.
/// </summary>
internal sealed class DefaultInstanceSetupService(
    IStoreManagerFactory storeManagerFactory,
    ITenantResolver tenantResolver,
    IServiceScopeFactory serviceScopeFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    ILocalAccountCreator localAccountCreator,
    IClientCreator clientCreator,
    ICryptoService cryptoService,
    TimeProvider timeProvider,
    ILogger<DefaultInstanceSetupService> logger
) : IInstanceSetupService
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private ITenantResolver TenantResolver { get; } = tenantResolver;

    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;

    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    private ILocalAccountCreator LocalAccountCreator { get; } = localAccountCreator;

    private IClientCreator ClientCreator { get; } = clientCreator;

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

    // The console registers as a confidential client that authenticates with a signed assertion, pushes its
    // authorization requests and holds sender-constrained tokens (ADR-1437 decision 6).
    private static JsonElement CreateManagementConsoleSettings(string baseUrl)
    {
        var origin = baseUrl.TrimEnd('/');

        return JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.TokenEndpointAuthMethod] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .PrivateKeyJwt,
                [OpenIdSettingNames.RequirePushedAuthorizationRequests] = true,
                [OpenIdSettingNames.RequireDPoPBoundAccessTokens] = true,
                [OpenIdSettingNames.GrantTypesSupported] = new[]
                {
                    OpenIdProtocol.GrantTypes.AuthorizationCode,
                    OpenIdProtocol.GrantTypes.RefreshToken,
                },
                [OpenIdSettingNames.ResponseTypesSupported] = new[]
                {
                    OpenIdProtocol.ResponseTypes.Code,
                },
                [OpenIdSettingNames.RedirectUris] = new[] { $"{origin}/bff/callback" },
                [OpenIdSettingNames.PostLogoutRedirectUris] = new[] { $"{origin}/" },
                [OpenIdSettingNames.BackChannelLogoutUri] = $"{origin}/bff/backchannel-logout",
                [OpenIdSettingNames.BackChannelLogoutSessionRequired] = true,
            }
        );
    }

    private static InstanceSetupResult CreateClientFailureResult(ClientCreation creation)
    {
        if (creation.Error is { } error)
            return new InstanceSetupResult { Error = error };

        var detail = string.Join(
            " ",
            creation.SettingErrors?.SelectMany(pair =>
                pair.Value.Select(message => $"{pair.Key}: {message}")
            )
                ?? []
        );

        return new InstanceSetupResult
        {
            Error = CreateError(
                StatusCodes.Status400BadRequest,
                $"The management console's client cannot be registered. {detail}"
            ),
        };
    }

    /// <inheritdoc />
    public async ValueTask<InstanceSetupResult> SetUpAsync(
        InstanceSetupRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var root = request.Root;

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

        (string ClientId, string BaseUrl, string Jwks)? consoleRegistration = null;
        if (request.ManagementConsole is { } console)
        {
            if (console.BaseUrl is not { } baseUrl || console.Jwks is not { } jwks)
            {
                return new InstanceSetupResult
                {
                    Error = CreateError(
                        StatusCodes.Status400BadRequest,
                        "The management console needs a base URL and a JWK Set."
                    ),
                };
            }

            consoleRegistration = (console.ClientId, baseUrl, jwks);
        }

        // Resolving the tenant creates the control-plane tenant on demand, so the principal and the role assignment
        // staged below have a tenant to reference even on a database that no replica has served. The resolver needs an
        // HTTP context only for its request-scoped services, so one is built over a scope of its own, as for other
        // work outside a request.
        string tenantId;
        await using (var scope = ServiceScopeFactory.CreateAsyncScope())
        {
            var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            var tenant = await TenantResolver.ResolveControlPlaneTenantAsync(
                httpContext,
                cancellationToken
            );
            tenantId = tenant.TenantId;
        }

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

        string? consoleClientId = null;
        if (consoleRegistration is { } registration)
        {
            var clientCreation = await ClientCreator.StageAsync(
                storeManager,
                new ClientCreationRequest
                {
                    TenantId = tenantId,
                    ClientId = registration.ClientId,
                    IsDisabled = false,
                    Settings = CreateManagementConsoleSettings(registration.BaseUrl),
                    Jwks = JsonSerializer.Deserialize<JsonElement>(registration.Jwks),
                },
                cancellationToken
            );
            if (!clientCreation.Succeeded)
                return CreateClientFailureResult(clientCreation);

            consoleClientId = clientCreation.Client.ClientId;
        }

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
            ManagementConsoleClientId = consoleClientId,
            Root = new CreateRootResponse
            {
                TenantId = tenantId,
                PrincipalId = creation.PrincipalId,
                UserName = root.UserName,
            },
        };
    }
}
