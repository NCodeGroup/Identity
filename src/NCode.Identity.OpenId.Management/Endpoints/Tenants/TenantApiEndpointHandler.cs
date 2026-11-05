// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.EncryptionKeys;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/*

POST  api/teams/{teamId}/tenants
GET   api/teams/{teamId}/tenants
GET   api/teams/{teamId}/tenants/{tenantId}/address

GET   api/tenants
GET   api/tenants/{tenantId}
GET   api/tenants/{tenantId}/address

GET   api/tenants/{tenantId}/settings
PATCH api/tenants/{tenantId}/settings
GET   api/tenants/{tenantId}/effective-settings

GET    api/tenants/{tenantId}/secrets
POST   api/tenants/{tenantId}/secrets
GET    api/tenants/{tenantId}/secrets/{secretId}
GET    api/tenants/{tenantId}/secrets/{secretId}/public-key
PUT    api/tenants/{tenantId}/secrets/{secretId}
DELETE api/tenants/{tenantId}/secrets/{secretId}

GET    api/tenants/{tenantId}/encryption-keys
POST   api/tenants/{tenantId}/encryption-keys/rekey

*/

/// <summary>
/// Provides the API endpoints for managing an OpenID Tenant, including its settings and secrets.
/// </summary>
internal class TenantApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ITenantValidator tenantValidator,
    ITenantResolver tenantResolver,
    ISecretValidator secretValidator,
    ISecretGenerator secretGenerator,
    IClientKeyImporter keyImporter,
    ISecretPublicKeyConverter secretPublicKeyConverter,
    ISecretProtectorProvider secretProtectorProvider,
    IMasterKeyManager masterKeyManager,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    ITenantEnvironmentCatalog tenantEnvironmentCatalog,
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<TenantApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ITenantValidator TenantValidator { get; } = tenantValidator;
    private ITenantResolver TenantResolver { get; } = tenantResolver;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IClientKeyImporter KeyImporter { get; } = keyImporter;
    private ISecretPublicKeyConverter SecretPublicKeyConverter { get; } = secretPublicKeyConverter;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IMasterKeyManager MasterKeyManager { get; } = masterKeyManager;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private ITenantEnvironmentCatalog TenantEnvironmentCatalog { get; } = tenantEnvironmentCatalog;
    private ITenantClientCoherenceChecker TenantClientCoherenceChecker { get; } =
        tenantClientCoherenceChecker;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<TenantApiEndpointHandler> Logger { get; } = logger;

    /// <summary>
    /// Gets the most days one read of a tenant's usage spans: a year of the ledger's retention.
    /// </summary>
    // See ADR-1199.
    internal const int MaxUsageDays = 366;

    /// <summary>
    /// Gets how many days before the last day a read of a tenant's usage starts when it names no first day.
    /// </summary>
    internal const int DefaultUsageDays = 30;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var tenants = endpoints.MapGroup("/tenants").WithTags(OpenIdConstants.EndpointTags.Tenants);

        var teamTenants = endpoints
            .MapGroup("/teams/{teamId}/tenants")
            .WithTags(OpenIdConstants.EndpointTags.Tenants);

        teamTenants
            .MapPost("", CreateTenantAsync)
            .Produces<TenantResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        teamTenants.MapGet("", ListTeamTenantsAsync).Produces<CollectionResource<TenantResource>>();
        teamTenants
            .MapGet("/{tenantId}/address", GetTeamTenantAddressAsync)
            .Produces<TenantAddressResource>();

        tenants.MapGet("", ListTenantsAsync).Produces<CollectionResource<TenantResource>>();
        tenants.MapGet("/{tenantId}", GetTenantAsync).Produces<TenantResource>();
        tenants.MapPatch("/{tenantId}", UpdateTenantAsync).Produces(StatusCodes.Status204NoContent);
        tenants
            .MapDelete("/{tenantId}", DeleteTenantAsync)
            .Produces(StatusCodes.Status204NoContent);
        tenants.MapGet("/{tenantId}/address", GetAddressAsync).Produces<TenantAddressResource>();
        tenants
            .MapGet("/{tenantId}/usage", GetUsageAsync)
            .Produces<TenantUsageResource>()
            .ProducesValidationProblem();

        tenants.MapGet("/{tenantId}/settings", GetSettingsAsync).Produces<TenantSettingsResource>();
        tenants
            .MapPatch("/{tenantId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        tenants
            .MapGet("/{tenantId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<TenantEffectiveSettingsResource>();

        tenants.MapGet("/{tenantId}/secrets", GetSecretsAsync).Produces<TenantSecretsResource>();
        tenants
            .MapPost("/{tenantId}/secrets", CreateSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        tenants
            .MapPost("/{tenantId}/secrets/import", ImportSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        tenants.MapGet("/{tenantId}/secrets/{secretId}", GetSecretAsync).Produces<SecretResource>();
        tenants
            .MapGet("/{tenantId}/secrets/{secretId}/public-key", GetPublicKeyAsync)
            .Produces<PublicKeyResource>();
        tenants
            .MapPut("/{tenantId}/secrets/{secretId}", UpdateSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
        tenants
            .MapDelete("/{tenantId}/secrets/{secretId}", DeleteSecretAsync)
            .Produces(StatusCodes.Status204NoContent);

        tenants
            .MapGet("/{tenantId}/encryption-keys", GetEncryptionKeysAsync)
            .Produces<EncryptionKeysResource>();
        tenants
            .MapPost("/{tenantId}/encryption-keys/rekey", RekeyEncryptionKeysAsync)
            .Produces<EncryptionKeyRotationResource>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        tenants
            .MapGet("/{tenantId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        tenants
            .MapPost("/{tenantId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        tenants
            .MapDelete("/{tenantId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Maps a <see cref="PersistedTenant"/> to its <see cref="TenantResource"/> representation, without its environment:
    /// the tenant's own state, as an audit record carries it.
    /// </summary>
    /// <param name="tenant">The <see cref="PersistedTenant"/> to map.</param>
    /// <returns>The mapped <see cref="TenantResource"/>.</returns>
    internal virtual TenantResource ToTenantResource(PersistedTenant tenant) =>
        ToTenantResource(tenant, environment: null);

    /// <summary>
    /// Maps a <see cref="PersistedTenant"/> to its <see cref="TenantResource"/> representation with its environment.
    /// </summary>
    /// <param name="tenant">The <see cref="PersistedTenant"/> to map.</param>
    /// <param name="environment">The tenant's effective environment, or <see langword="null"/> when it cannot be
    /// determined.</param>
    /// <returns>The mapped <see cref="TenantResource"/>.</returns>
    internal virtual TenantResource ToTenantResource(
        PersistedTenant tenant,
        TenantEnvironmentResource? environment
    ) =>
        new()
        {
            TenantId = tenant.TenantId,
            TeamId = tenant.TeamId,
            ConcurrencyToken = tenant.ConcurrencyToken,
            Issuer = tenant.Issuer,
            MtlsEndpointAliasAuthority = tenant.MtlsEndpointAliasAuthority,
            IsDisabled = tenant.IsDisabled,
            DisplayName = tenant.DisplayName,
            Environment = environment,
        };

    /// <summary>
    /// Creates a mapper from a <see cref="PersistedTenant"/> to its <see cref="TenantResource"/> that resolves each
    /// tenant's effective environment under its team's effective settings, which it reads once for each team the
    /// tenants belong to. When a team's stored settings cannot be honored, the mapper leaves the environment of that
    /// team's tenants unset rather than failing the request.
    /// </summary>
    /// <param name="tenants">The tenants the mapper will map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The mapper.</returns>
    // See ADR-1289.
    internal virtual async ValueTask<
        Func<PersistedTenant, TenantResource>
    > CreateTenantResourceMapperAsync(
        IEnumerable<PersistedTenant> tenants,
        CancellationToken cancellationToken
    )
    {
        var teamSettings = new Dictionary<string, IReadOnlySettingCollection?>(
            StringComparer.Ordinal
        );
        foreach (var teamId in tenants.Select(tenant => tenant.TeamId))
        {
            if (teamSettings.ContainsKey(teamId))
            {
                continue;
            }

            try
            {
                teamSettings[teamId] = await EffectiveSettingsResolver.ResolveTeamAsync(
                    teamId,
                    cancellationToken
                );
            }
            catch (UnreadableSettingsException exception)
            {
                LogEnvironmentUnresolved(exception);
                teamSettings[teamId] = null;
            }
        }

        return tenant =>
            teamSettings.TryGetValue(tenant.TeamId, out var settings) && settings is not null
                ? ToTenantResource(tenant, ResolveEnvironment(settings, tenant))
                : ToTenantResource(tenant);
    }

    /// <summary>
    /// Resolves a tenant's effective environment from its stored settings under its team's effective settings.
    /// </summary>
    /// <param name="teamSettings">The tenant's team's effective settings.</param>
    /// <param name="tenant">The tenant whose environment to resolve.</param>
    /// <returns>The tenant's environment, or <see langword="null"/> when its stored settings cannot be honored or name a
    /// type that is not registered.</returns>
    // A tenant whose settings need repair is still listed, so its environment is best-effort (ADR-1245, ADR-1289).
    internal virtual TenantEnvironmentResource? ResolveEnvironment(
        IReadOnlySettingCollection teamSettings,
        PersistedTenant tenant
    )
    {
        IReadOnlySettingCollection tenantSettings;
        try
        {
            tenantSettings = EffectiveSettingsResolver.ResolveStored(
                teamSettings,
                OpenIdSettingScopes.Tenant,
                tenant.TenantId,
                tenant.Settings.Value
            );
        }
        catch (UnreadableSettingsException exception)
        {
            LogEnvironmentUnresolved(exception);
            return null;
        }

        var typeName = tenantSettings.GetValue(OpenIdSettingKeys.EnvironmentType);
        if (!TenantEnvironmentCatalog.TryGetType(typeName, out var type))
        {
            return null;
        }

        var name = tenantSettings.GetValue(OpenIdSettingKeys.EnvironmentName);
        return new TenantEnvironmentResource
        {
            Type = type.TypeName,
            Name = string.IsNullOrEmpty(name) ? null : name,
            IsProduction = type.IsProduction,
        };
    }

    private void LogEnvironmentUnresolved(UnreadableSettingsException exception) =>
        Logger.TenantEnvironmentUnresolved(
            exception.ScopeName,
            exception.OwnerId,
            exception.DocumentError
        );

    /// <summary>
    /// Maps a <see cref="PersistedTenantSettings"/> to its <see cref="TenantSettingsResource"/> representation.
    /// </summary>
    /// <param name="settings">The <see cref="PersistedTenantSettings"/> to map.</param>
    /// <returns>The mapped <see cref="TenantSettingsResource"/>.</returns>
    internal virtual TenantSettingsResource ToTenantSettingsResource(
        PersistedTenantSettings settings
    )
    {
        return new TenantSettingsResource
        {
            TenantId = settings.TenantId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };
    }

    /// <summary>
    /// Maps a tenant's effective (merged) <see cref="IReadOnlySettingCollection"/> to its
    /// <see cref="TenantEffectiveSettingsResource"/> representation: a flat JSON object of setting name to formatted
    /// value, using the same representation as the discovery document, ordered by name for a stable dump.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the settings.</param>
    /// <param name="settings">The effective <see cref="IReadOnlySettingCollection"/> to map.</param>
    /// <returns>The mapped <see cref="TenantEffectiveSettingsResource"/>.</returns>
    internal virtual TenantEffectiveSettingsResource ToTenantEffectiveSettingsResource(
        string tenantId,
        IReadOnlySettingCollection settings
    )
    {
        return new TenantEffectiveSettingsResource
        {
            TenantId = tenantId,
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.Tenant
            ),
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedTenantSecrets"/> to its <see cref="TenantSecretsResource"/> representation.
    /// </summary>
    /// <param name="secrets">The <see cref="PersistedTenantSecrets"/> to map.</param>
    /// <returns>The mapped <see cref="TenantSecretsResource"/>.</returns>
    internal virtual TenantSecretsResource ToTenantSecretsResource(PersistedTenantSecrets secrets)
    {
        return new TenantSecretsResource
        {
            TenantId = secrets.TenantId,
            ConcurrencyToken = secrets.ConcurrencyToken,
            Secrets = ToSecretsResource(secrets.Value),
        };
    }

    /// <summary>
    /// Lists the OpenID tenants.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of tenants to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of tenants.</response>
    [EndpointName("api/tenants/list")]
    internal virtual async ValueTask<IResult> ListTenantsAsync(
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        // The page is fetched only after authorization, so the teams' settings are read with it, never for a caller
        // who may not list tenants.
        Func<PersistedTenant, TenantResource> toTenantResource = ToTenantResource;

        // Listing tenants across teams is platform authority: a global admin or an assignment at the platform node
        // lists them, and nothing beneath the platform does.
        return await ProcessListAsync(
            httpContext,
            GetPlatformNode(httpContext),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ITenantStore>();
                var page = await store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
                toTenantResource = await CreateTenantResourceMapperAsync(
                    page.Items,
                    cancellationToken
                );
                return page;
            },
            tenant => toTenantResource(tenant)
        );
    }

    /// <summary>
    /// Lists the OpenID tenants of a team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of tenants to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of the team's tenants.</response>
    [EndpointName("api/teams/tenants/list")]
    internal virtual async ValueTask<IResult> ListTeamTenantsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);
        Func<PersistedTenant, TenantResource> toTenantResource = ToTenantResource;

        return await ProcessListAsync(
            httpContext,
            GetTeamNode(httpContext, teamId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ITenantStore>();
                var page = await store.GetPageAsync(
                    teamId,
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
                toTenantResource = await CreateTenantResourceMapperAsync(
                    page.Items,
                    cancellationToken
                );
                return page;
            },
            tenant => toTenantResource(tenant)
        );
    }

    // The platform's authority node, addressed from the control-plane tenant the request runs under.
    private static ResourceNode GetPlatformNode(HttpContext httpContext) =>
        ResourceNode.For(
            httpContext.GetOpenIdContext().Tenant.TenantId,
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId
        );

    // The team's authority node, addressed from the control-plane tenant the request runs under.
    private static ResourceNode GetTeamNode(HttpContext httpContext, string teamId) =>
        ResourceNode.For(
            httpContext.GetOpenIdContext().Tenant.TenantId,
            ResourceNodeTypes.Team,
            teamId
        );

    /// <summary>
    /// Gets an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested tenant.</response>
    [EndpointName("api/tenants/get")]
    internal virtual async ValueTask<IResult> GetTenantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);

        // The environment is resolved only after authorization (ADR-1245).
        return await ProcessGetAsync(
            httpContext,
            tenant,
            node,
            Operations.Read,
            async (persistedTenant, token) =>
            {
                var toTenantResource = await CreateTenantResourceMapperAsync(
                    [persistedTenant],
                    token
                );
                return toTenantResource(persistedTenant);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Creates an OpenID tenant under a team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team that will hold the tenant.</param>
    /// <param name="request">The <see cref="CreateTenantRequest"/> describing the tenant to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created tenant.</response>
    /// <response code="404">The team does not exist.</response>
    /// <response code="409">The team holds its maximum number of tenants.</response>
    [EndpointName("api/teams/tenants/create")]
    internal virtual async ValueTask<IResult> CreateTenantAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();
        var teamStore = storeManager.GetStore<ITeamStore>();

        var team = await teamStore.GetOrDefaultAsync(teamId, cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        var controlPlaneTenantId = openIdContext.Tenant.TenantId;

        // A tenant is named by its creator (ADR-1159); the validator checks the name with the rest of its address.
        var tenantId = request.TenantId;

        // A tenant written without an issuer gets the one its strategy derives for it, judged like an explicit one
        // (ADR-1165).
        var issuer =
            NullIfEmpty(request.Issuer)
            ?? await TenantResolver.GetDefaultIssuerAsync(tenantId, cancellationToken);
        var mtlsEndpointAliasAuthority =
            NullIfEmpty(request.MtlsEndpointAliasAuthority)
            ?? await TenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync(
                tenantId,
                cancellationToken
            );

        var tenant = new PersistedTenant
        {
            TenantId = tenantId,
            TeamId = teamId,
            ConcurrencyToken = string.Empty,
            Issuer = issuer,
            MtlsEndpointAliasAuthority = mtlsEndpointAliasAuthority,
            IsDisabled = request.IsDisabled,
            DisplayName = request.DisplayName,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = SerializeToElement(ToJsonObject(request.Settings)),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        // Core preconditions (authorization + the tenant's address + uniqueness) are asserted by the validator
        // (ADR-1014, ADR-1160).
        var error = await TenantValidator.ValidateCreateAsync(
            httpContext.User,
            controlPlaneTenantId,
            tenant,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // A team holds at most its 'max_tenants' tenants; unset means unlimited (ADR-1411).
        var teamSettings = await EffectiveSettingsResolver.ResolveTeamAsync(
            teamId,
            cancellationToken
        );
        if (
            teamSettings.TryGetValue(OpenIdSettingKeys.MaxTenants, out var maxTenants)
            && await store.CountAsync(teamId, cancellationToken) >= maxTenants
        )
        {
            return ToErrorResult(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = $"The team holds its maximum of {maxTenants} tenants.",
                }
            );
        }

        // Setting names are a closed vocabulary (ADR-1139), and a tenant carries only the settings that apply within it
        // (ADR-1149): a settings document that cannot be honored is rejected here rather than stored and failed on every
        // request that resolves the tenant.
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.Tenant,
            openIdContext.Environment.JsonSerializerOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        await store.AddAsync(tenant, cancellationToken);

        // The tenant row commits first: the creator's ownership is a row the tenant owns, and the stores resolve an
        // owning tenant from the database rather than the change tracker — the same reason the provisioner commits a
        // tenant before seeding it.
        await storeManager.SaveChangesAsync(cancellationToken);

        await ResourceOwnershipService.AssignCreatorAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            tenant.TenantId,
            ResourceNodeTypes.Tenant,
            tenant.TenantId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        // The tenant is seeded as soon as it exists — after the root tenant — so its system resource servers and keys
        // are in place before its first request, through the one pipeline every tenant is seeded by (ADR-1166). The
        // provisioner is scoped (its mediator is request-scoped), so it is resolved from the request's services.
        var provisioner = httpContext.RequestServices.GetRequiredService<ITenantProvisioner>();
        await provisioner.SeedAsync(tenant.TenantId, cancellationToken);

        // A seed contributor may write to the tenant row (a seeded signing key changes its secrets), which replaces its
        // concurrency token, so the response reflects the tenant as it now stands.
        tenant = await ReloadTenantAsync(tenant, cancellationToken);

        // The audit record carries the tenant's own state, as an update's and a deletion's do; the environment is
        // derived from its settings and stamps the audit event itself (ADR-1289).
        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenant.TeamId,
            tenant.TenantId,
            ResourceChangeTypes.Created,
            ToResourceValues(ToTenantResource(tenant)),
            cancellationToken
        );

        var toTenantResource = await CreateTenantResourceMapperAsync([tenant], cancellationToken);
        var resource = toTenantResource(tenant);

        httpContext.Response.Headers.ETag = tenant.ConcurrencyToken;
        return TypedResults.Created($"/tenants/{tenant.TenantId}", resource);
    }

    /// <summary>
    /// Previews the address of an existing OpenID tenant: the issuer and mutual-TLS endpoint-alias authority it has, and
    /// every reason it could not be served there. A caller checks a change to a tenant's addresses with it before making
    /// the change; the same rules judge the write itself.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="issuer">The proposed issuer; when omitted, the tenant's current issuer, or for a tenant without one
    /// the issuer its strategy derives for it.</param>
    /// <param name="mtlsEndpointAliasAuthority">The proposed mutual-TLS endpoint-alias authority; when omitted, the
    /// tenant's current alias, or for a tenant without one the alias its strategy derives for it.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's address and the reasons it cannot be served there, if any.</response>
    /// <response code="404">The tenant does not exist.</response>
    // See ADR-1165 (the address is previewed through the seam that judges it).
    [EndpointName("api/tenants/address")]
    internal virtual async ValueTask<IResult> GetAddressAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? issuer,
        [FromQuery] string? mtlsEndpointAliasAuthority,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Read
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        return await PreviewAddressAsync(
            tenant,
            true,
            tenantId,
            issuer,
            mtlsEndpointAliasAuthority,
            storeManager,
            cancellationToken
        );
    }

    /// <summary>
    /// Previews the address an OpenID tenant would have if a team created it: the issuer and mutual-TLS endpoint-alias
    /// authority it would have, and every reason it could not be served there. A caller checks a new tenant's name and
    /// addresses with it before creating the tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team that would hold the tenant.</param>
    /// <param name="tenantId">The identifier the tenant would have.</param>
    /// <param name="issuer">The proposed issuer; when omitted, the issuer the tenant's strategy derives for it.</param>
    /// <param name="mtlsEndpointAliasAuthority">The proposed mutual-TLS endpoint-alias authority; when omitted, the alias
    /// the tenant's strategy derives for it.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The address the tenant would have and the reasons it cannot be served there, if any.</response>
    /// <response code="404">The team does not exist.</response>
    // See ADR-1165 (the address is previewed through the seam that judges it).
    [EndpointName("api/teams/tenants/address/get")]
    internal virtual async ValueTask<IResult> GetTeamTenantAddressAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromRoute] string tenantId,
        [FromQuery] string? issuer,
        [FromQuery] string? mtlsEndpointAliasAuthority,
        CancellationToken cancellationToken
    )
    {
        // Previewing a new tenant is part of creating it, which is authority at the team node (ADR-1034).
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetTeamNode(httpContext, teamId),
            Operations.Create
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var team = await storeManager
            .GetStore<ITeamStore>()
            .GetOrDefaultAsync(teamId, cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        var exists =
            await storeManager
                .GetStore<ITenantStore>()
                .GetOrDefaultAsync(tenantId, cancellationToken)
            is not null;

        return await PreviewAddressAsync(
            null,
            exists,
            tenantId,
            issuer,
            mtlsEndpointAliasAuthority,
            storeManager,
            cancellationToken
        );
    }

    private async ValueTask<IResult> PreviewAddressAsync(
        PersistedTenant? tenant,
        bool exists,
        string tenantId,
        string? issuer,
        string? mtlsEndpointAliasAuthority,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var proposedIssuer = NullIfEmpty(issuer) ?? NullIfEmpty(tenant?.Issuer);
        var effectiveIssuer =
            proposedIssuer
            ?? await TenantResolver.GetDefaultIssuerAsync(tenantId, cancellationToken);

        var proposedAlias =
            NullIfEmpty(mtlsEndpointAliasAuthority)
            ?? NullIfEmpty(tenant?.MtlsEndpointAliasAuthority);
        var effectiveAlias =
            proposedAlias
            ?? await TenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync(
                tenantId,
                cancellationToken
            );

        var failures = await TenantResolver.ValidateTenantAsync(
            tenantId,
            effectiveIssuer,
            effectiveAlias,
            storeManager,
            cancellationToken
        );

        return TypedResults.Ok(
            new TenantAddressResource
            {
                TenantId = tenantId,
                Issuer = effectiveIssuer,
                IsIssuerDerived = proposedIssuer is null && effectiveIssuer is not null,
                MtlsEndpointAliasAuthority = effectiveAlias,
                IsMtlsEndpointAliasAuthorityDerived =
                    proposedAlias is null && effectiveAlias is not null,
                Exists = exists,
                Failures = failures
                    .Select(failure => new TenantAddressFailure
                    {
                        Detail = failure.Detail,
                        IsConflict = failure.IsConflict,
                    })
                    .ToList(),
            }
        );
    }

    /// <summary>
    /// Updates an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The JSON Patch document to apply to the tenant metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The tenant was updated.</response>
    [EndpointName("api/tenants/update")]
    internal virtual async ValueTask<IResult> UpdateTenantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] JsonPatchDocument<UpdateTenantRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var model = new UpdateTenantRequest
        {
            Issuer = tenant.Issuer,
            MtlsEndpointAliasAuthority = tenant.MtlsEndpointAliasAuthority,
            DisplayName = tenant.DisplayName,
            IsDisabled = tenant.IsDisabled,
        };

        try
        {
            request.ApplyTo(model);
        }
        catch (JsonPatchException exception)
        {
            Logger.JsonPatchFailed(exception);
            return TypedResults.Problem(
                detail: "The JSON Patch document could not be applied.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // An update that clears the issuer or the alias restores the one the tenant's strategy derives for it (ADR-1165,
        // ADR-1195).
        model.Issuer =
            NullIfEmpty(model.Issuer)
            ?? await TenantResolver.GetDefaultIssuerAsync(tenant.TenantId, cancellationToken);
        model.MtlsEndpointAliasAuthority =
            NullIfEmpty(model.MtlsEndpointAliasAuthority)
            ?? await TenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync(
                tenant.TenantId,
                cancellationToken
            );

        // Core preconditions (authorization + If-Match + required fields + the tenant's address + issuer uniqueness)
        // are asserted by the validator against the hydrated model (ADR-1014, ADR-1160).
        var error = await TenantValidator.ValidateUpdateAsync(
            httpContext.User,
            tenant,
            model,
            ifMatch,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        tenant.Issuer = model.Issuer;
        tenant.MtlsEndpointAliasAuthority = model.MtlsEndpointAliasAuthority;
        tenant.IsDisabled = model.IsDisabled;
        tenant.DisplayName = model.DisplayName;

        await store.UpdateAsync(tenant, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenant.TeamId,
            tenant.TenantId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToTenantResource(tenant)),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenant.TenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The tenant was deleted.</response>
    /// <response code="409">The tenant still has clients or resource servers, or has deletion protection enabled, and cannot be deleted.</response>
    [EndpointName("api/tenants/delete")]
    internal virtual async ValueTask<IResult> DeleteTenantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        // Core preconditions (authorization + no-dependents) are asserted by the validator (ADR-1014).
        var error = await TenantValidator.ValidateDeleteAsync(
            httpContext.User,
            tenant,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var effectiveSettings = await MergeTenantSettingsAsync(
            tenant.TeamId,
            tenant.TenantId,
            tenant.Settings.Value,
            cancellationToken
        );
        if (effectiveSettings.GetValue(OpenIdSettingKeys.DeletionProtection))
        {
            return ToErrorResult(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail =
                        "The tenant has deletion protection enabled. Set its 'deletion_protection' setting to false, then delete it.",
                }
            );
        }

        var resourceValues = ToResourceValues(ToTenantResource(tenant));

        // The tenant's row is gone when the deletion is audited, so its environment type is recorded now (ADR-1289).
        ManagementTenantEnvironment.Remember(
            httpContext,
            tenant.TenantId,
            effectiveSettings.GetValue(OpenIdSettingKeys.EnvironmentType)
        );

        await store.RemoveAsync(tenantId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenant.TeamId,
            tenant.TenantId,
            ResourceChangeTypes.Deleted,
            resourceValues,
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenant.TenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a tenant's usage from the durable daily ledger: the count of each meter on each UTC calendar day of a range,
    /// which the platform bills a plan by and reports to the customer. The ledger keeps a deleted tenant's usage, so it
    /// can still be read.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="from">The first UTC calendar day, inclusive; when omitted, 30 days before <paramref name="to"/>.</param>
    /// <param name="to">The last UTC calendar day, inclusive; when omitted, the current UTC day.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's usage on the range.</response>
    /// <response code="400">The range is reversed, longer than <see cref="MaxUsageDays"/> days, or would start before the
    /// first representable day.</response>
    // See ADR-1199.
    [EndpointName("api/tenants/usage/get")]
    internal virtual async ValueTask<IResult> GetUsageAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Read
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var lastDay = to ?? DateOnly.FromDateTime(TimeProvider.GetUtcNow().UtcDateTime);
        if (from is null && lastDay.DayNumber < DefaultUsageDays)
        {
            // The default range would start before DateOnly.MinValue, which AddDays refuses by throwing.
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["to"] =
                    [
                        $"Without 'from', 'to' must be at least {DefaultUsageDays} days after the first representable day.",
                    ],
                }
            );
        }

        var firstDay = from ?? lastDay.AddDays(-DefaultUsageDays);
        if (firstDay > lastDay || lastDay.DayNumber - firstDay.DayNumber >= MaxUsageDays)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["from"] =
                    [
                        $"The range must run forward from 'from' to 'to' and span at most {MaxUsageDays} days.",
                    ],
                }
            );
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantUsageStore>();
        var usage = await store.ListAsync(tenantId, firstDay, lastDay, cancellationToken);

        return TypedResults.Ok(
            new TenantUsageResource
            {
                TenantId = tenantId,
                From = firstDay,
                To = lastDay,
                Entries = usage
                    .Select(entry => new TenantUsageEntry
                    {
                        Day = entry.Day,
                        Meter = entry.Meter,
                        Count = entry.Count,
                    })
                    .ToList(),
            }
        );
    }

    /// <summary>
    /// Gets a tenant's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's settings.</response>
    [EndpointName("api/tenants/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantSettings = await store.GetSettingsOrDefaultAsync(tenantId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            tenantSettings,
            node,
            Operations.Read,
            ToTenantSettingsResource
        );
    }

    /// <summary>
    /// Gets a tenant's effective (resolved) settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's effective settings.</response>
    [EndpointName("api/tenants/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);

        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // See ADR-1245.
        return await ProcessGetAsync(
            httpContext,
            tenant.Settings.Value,
            node,
            Operations.Read,
            async (settings, token) =>
            {
                var effectiveSettings = await MergeTenantSettingsAsync(
                    tenant.TeamId,
                    tenantId,
                    settings,
                    token
                );
                return ToTenantEffectiveSettingsResource(tenantId, effectiveSettings);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Merges a tenant's persisted settings onto its team's effective settings through the
    /// <see cref="IEffectiveSettingsResolver"/>, producing the same effective <see cref="IReadOnlySettingCollection"/>
    /// the authorization server resolves for the tenant at runtime — including the ceilings of the security profiles the
    /// tenant selects. A document that cannot be honored fails naming the tenant.
    /// </summary>
    /// <param name="teamId">The identifier of the team that owns the tenant.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the settings document.</param>
    /// <param name="tenantSettings">The tenant's settings document to merge.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The tenant's effective <see cref="IReadOnlySettingCollection"/>.</returns>
    /// <exception cref="UnreadableSettingsException">The tenant's document, or its team's, cannot be honored.</exception>
    // See ADR-1204 and ADR-1245.
    internal virtual async ValueTask<IReadOnlySettingCollection> MergeTenantSettingsAsync(
        string teamId,
        string tenantId,
        JsonElement tenantSettings,
        CancellationToken cancellationToken
    )
    {
        var teamEffective = await EffectiveSettingsResolver.ResolveTeamAsync(
            teamId,
            cancellationToken
        );
        return EffectiveSettingsResolver.ResolveStored(
            teamEffective,
            OpenIdSettingScopes.Tenant,
            tenantId,
            tenantSettings
        );
    }

    /// <summary>
    /// Verifies that replacing a tenant's settings document leaves every value each of its clients registers permitted by
    /// every client coherence rule (<see cref="ITenantClientCoherenceChecker"/>). The clients are not walked when the
    /// change leaves the effective value of every setting a rule reads unchanged. When the replaced document cannot be
    /// honored, no client is checked, because none was coherent under it.
    /// </summary>
    /// <param name="teamId">The identifier of the team that owns the tenant.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant whose settings change.</param>
    /// <param name="currentSettings">The tenant's current settings document.</param>
    /// <param name="newSettings">The tenant's new settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenant's clients.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances, keyed by the tenant setting that does not permit each value, or
    /// an empty list.</returns>
    // See ADR-1206 and ADR-1241.
    internal virtual async ValueTask<IReadOnlyList<SettingError>> ValidateClientCoherenceAsync(
        string teamId,
        string tenantId,
        JsonElement currentSettings,
        JsonElement newSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var teamSettings = await EffectiveSettingsResolver.ResolveTeamAsync(
            teamId,
            cancellationToken
        );
        var newEffective = EffectiveSettingsResolver.Resolve(
            teamSettings,
            OpenIdSettingScopes.Tenant,
            newSettings
        );

        IReadOnlySettingCollection currentEffective;
        try
        {
            currentEffective = EffectiveSettingsResolver.Resolve(
                teamSettings,
                OpenIdSettingScopes.Tenant,
                currentSettings
            );
        }
        catch (JsonException exception)
        {
            Logger.TenantSettingsUnreadable(tenantId, exception.Message);
            return [];
        }

        var findings = await TenantClientCoherenceChecker.FindIntroducedViolationsAsync(
            storeManager,
            tenantId,
            currentEffective,
            newEffective,
            cancellationToken
        );

        return findings
            .Select(finding => new SettingError(
                finding.Violation.GoverningSettingName,
                $"The change would make the client '{finding.ClientId}' incoherent: {finding.Violation.Description}"
            ))
            .ToList();
    }

    /// <summary>
    /// Updates a tenant's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The tenant's settings were updated.</response>
    [EndpointName("api/tenants/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantSettings = await store.GetSettingsOrDefaultAsync(tenantId, cancellationToken);

        if (tenantSettings is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenantSettings,
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, tenantSettings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(tenantSettings.Value);

        try
        {
            request.ApplyTo(jsonObject);
        }
        catch (JsonPatchException exception)
        {
            Logger.JsonPatchFailed(exception);
            return TypedResults.Problem(
                detail: "The JSON Patch document could not be applied.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var settingsValue = SerializeToElement(jsonObject);
        var jsonOptions = httpContext.GetOpenIdContext().Environment.JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(
            settingsValue,
            OpenIdSettingScopes.Tenant,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var coherenceErrors = await ValidateClientCoherenceAsync(
            tenant.TeamId,
            tenantId,
            tenantSettings.Value,
            settingsValue,
            storeManager,
            cancellationToken
        );
        if (coherenceErrors.Count > 0)
        {
            return SettingsValidationProblem(coherenceErrors);
        }

        tenantSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(tenantSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenant.TeamId,
            tenantSettings.TenantId,
            ResourceChangeTypes.SettingsUpdated,
            ToResourceValues(ToTenantSettingsResource(tenantSettings)),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantSettings.TenantId,
            settingsChanged: true,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = tenantSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a tenant's secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's secrets.</response>
    [EndpointName("api/tenants/secrets/get")]
    internal virtual async ValueTask<IResult> GetSecretsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantSecrets = await store.GetSecretsOrDefaultAsync(tenantId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            tenantSecrets,
            node,
            Operations.Read,
            ToTenantSecretsResource
        );
    }

    /// <summary>
    /// Creates a tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The <see cref="CreateSecretRequest"/> describing the secret to generate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created secret.</response>
    [EndpointName("api/tenants/secrets/create")]
    internal virtual async ValueTask<IResult> CreateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenant,
            Operations.Create
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validation = SecretValidator.ValidateCreate(request, createdWhen);
        if (!validation.IsValid)
        {
            return ToErrorResult(validation.Error);
        }

        var secretId = CryptoService.GenerateResourceId();

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(tenantId),
            [],
            cancellationToken
        );

        GeneratedSecret generatedSecret;
        try
        {
            generatedSecret = SecretGenerator.GenerateSecret(
                protector,
                new GenerateSecretRequest
                {
                    SecretId = secretId,
                    SecretType = request.SecretType,
                    GenerationParameters = validation.GenerationParameters,
                    Use = request.Use,
                    Algorithm = request.Algorithm,
                    CreatedWhen = createdWhen,
                    ActivatesWhen = request.ActivatesWhen,
                    RetiresWhen = request.RetiresWhen,
                    ExpiresWhen = request.ExpiresWhen,
                }
            );
        }
        catch (Exception exception)
            when (exception is ArgumentException or InvalidOperationException)
        {
            Logger.SecretGenerationFailed(exception);
            return TypedResults.Problem(
                detail: "The secret could not be generated from the supplied parameters.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        try
        {
            await store.AddSecretAsync(tenantId, generatedSecret.Secret, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: "A secret with the specified identifier already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        var secretResource = ToSecretResource(generatedSecret.Secret);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(secretResource),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        // The store assigns the secret's token on insert (ADR-1012), so no re-read is needed.
        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;
        return TypedResults.Created($"/tenants/{tenantId}/secrets/{secretId}", secretResource);
    }

    /// <summary>
    /// Imports a caller-supplied public key (JWK, PEM, or certificate) as a new tenant secret.
    /// </summary>
    // See ADR-1104.
    internal virtual async ValueTask<IResult> ImportSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] ImportSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenant,
            Operations.Create
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validationError = SecretValidator.ValidateImport(request, createdWhen);
        if (validationError is not null)
        {
            return ToErrorResult(validationError);
        }

        var secretId = CryptoService.GenerateResourceId();

        var importRequest = new SecretKeyImportRequest
        {
            SecretId = secretId,
            SecretType = request.SecretType,
            Format = request.Format,
            KeyMaterial = request.KeyMaterial,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = createdWhen,
            ActivatesWhen = request.ActivatesWhen,
            RetiresWhen = request.RetiresWhen,
            ExpiresWhen = request.ExpiresWhen,
        };
        if (!KeyImporter.TryImportKey(importRequest, out var importedSecret, out var importError))
        {
            return TypedResults.Problem(
                detail: importError,
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var keyIdConflict = KeyIdConflictOrDefault(tenant.Secrets.Value, importedSecret, "tenant");
        if (keyIdConflict is not null)
        {
            return keyIdConflict;
        }

        try
        {
            await store.AddSecretAsync(tenantId, importedSecret, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: "A secret with the specified identifier already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        var importedSecretResource = ToSecretResource(importedSecret);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(importedSecretResource),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = importedSecret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/secrets/{secretId}",
            importedSecretResource
        );
    }

    /// <summary>
    /// Gets a tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested secret.</response>
    [EndpointName("api/tenants/secrets/get-one")]
    internal virtual async ValueTask<IResult> GetSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var secret = await store.GetSecretOrDefaultAsync(tenantId, secretId, cancellationToken);
        var scoped = TenantOwnedResource.ForOrDefault(tenantId, secret);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            scoped,
            node,
            Operations.Read,
            x => ToSecretResource(x.Value)
        );
    }

    /// <summary>
    /// Gets the public key material of an asymmetric tenant secret, as a JWK and a PEM.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The public key material of the requested secret.</response>
    [EndpointName("api/tenants/secrets/get-public-key")]
    internal virtual async ValueTask<IResult> GetPublicKeyAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var secret = await store.GetSecretOrDefaultAsync(tenantId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        var scoped = TenantOwnedResource.ForOrDefault(tenantId, secret);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            scoped,
            node,
            Operations.Read,
            // The public key is read from its stored public half, so no master key is loaded (ADR-1268).
            x => SecretPublicKeyConverter.Convert(x.Value)
        );
    }

    /// <summary>
    /// Updates a tenant secret's metadata.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> with the new metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was updated.</response>
    [EndpointName("api/tenants/secrets/update")]
    internal virtual async ValueTask<IResult> UpdateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string secretId,
        [FromBody] UpdateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var secret = await store.GetSecretOrDefaultAsync(tenantId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(tenantId, secret),
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, secret.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var metadataError = SecretValidator.ValidateUpdate(secret, request);
        if (metadataError is not null)
        {
            return ToErrorResult(metadataError);
        }

        var updated = WithUpdate(secret, request);

        await store.UpdateSecretAsync(tenantId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToSecretResource(updated)),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was deleted.</response>
    [EndpointName("api/tenants/secrets/delete")]
    internal virtual async ValueTask<IResult> DeleteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var secret = await store.GetSecretOrDefaultAsync(tenantId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(tenantId, secret),
            Operations.Delete
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var secretValues = ToResourceValues(ToSecretResource(secret));

        await store.RemoveSecretAsync(tenantId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Deleted,
            secretValues,
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a tenant's owners.
    /// </summary>
    /// <response code="200">A page of the tenant's owners.</response>
    [EndpointName("api/tenants/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessListOwnersAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Tenant,
            tenantId,
            (storeManager, token) => TenantExistsAsync(storeManager, tenantId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Adds an owner to a tenant.
    /// </summary>
    /// <response code="201">The owner was added.</response>
    [EndpointName("api/tenants/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    )
    {
        return await ProcessAddOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Tenant,
            tenantId,
            request,
            $"/tenants/{tenantId}/owners",
            (storeManager, token) => TenantExistsAsync(storeManager, tenantId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Removes an owner from a tenant.
    /// </summary>
    /// <response code="204">The owner was removed.</response>
    [EndpointName("api/tenants/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessRemoveOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Tenant,
            tenantId,
            principalId,
            cancellationToken
        );
    }

    // Announces a committed change to the tenant: this replica evicts it from the tenant cache, so the change takes effect
    // at once, and seeds it after a settings change, so a key its new settings require is provisioned (ADR-1169). The
    // mediator is request-scoped, so it is resolved from the request's services.
    private static ValueTask NotifyTenantChangedAsync(
        HttpContext httpContext,
        string tenantId,
        bool settingsChanged,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return mediator.SendAsync(
            new TenantChangedCommand(tenantId, settingsChanged),
            cancellationToken
        );
    }

    private static async ValueTask<bool> TenantExistsAsync(
        IStoreManager storeManager,
        string tenantId,
        CancellationToken cancellationToken
    ) =>
        await storeManager.GetStore<ITenantStore>().GetOrDefaultAsync(tenantId, cancellationToken)
            is not null;

    private async ValueTask<PersistedTenant> ReloadTenantAsync(
        PersistedTenant tenant,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();
        return await store.GetOrDefaultAsync(tenant.TenantId, cancellationToken) ?? tenant;
    }

    // An empty issuer means the tenant has none; storing it as empty would put it under the issuer's unique index.
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// Lists the master keys of a tenant, newest version first. The response carries no key material.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's encryption keys.</response>
    /// <response code="404">The tenant does not exist.</response>
    [EndpointName("api/tenants/encryption-keys/get")]
    internal virtual async ValueTask<IResult> GetEncryptionKeysAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenant = await FindTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenant,
            Operations.Read
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        return await EncryptionKeyEndpoints.GetAsync(
            MasterKeyManager,
            SecretOwner.ForTenant(tenant.TenantId),
            cancellationToken
        );
    }

    /// <summary>
    /// Rekeys a tenant: retires its active master key and creates the next version, whose values are then rewrapped in
    /// the background.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The new active key and the key that it retired.</response>
    /// <response code="404">The tenant does not exist.</response>
    /// <response code="409">The last rotation is younger than twice the master key refresh interval (the response carries Retry-After).</response>
    /// <response code="412">Another rekey of the same master key committed first.</response>
    [EndpointName("api/tenants/encryption-keys/rekey")]
    internal virtual async ValueTask<IResult> RekeyEncryptionKeysAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenant = await FindTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenant,
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        return await EncryptionKeyEndpoints.RekeyAsync(
            MasterKeyManager,
            ManagementAuditRecorder,
            httpContext,
            SecretOwner.ForTenant(tenant.TenantId),
            cancellationToken
        );
    }

    private async ValueTask<PersistedTenant?> FindTenantAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        return await storeManager
            .GetStore<ITenantStore>()
            .GetOrDefaultAsync(tenantId, cancellationToken);
    }
}
