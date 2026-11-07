// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.AspNetCore;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/*

POST  api/teams/{teamId}/clients
GET   api/teams/{teamId}/clients
POST  api/platform/clients
GET   api/platform/clients

GET   api/tenants/{tenantId}/clients/{clientId}

GET   api/tenants/{tenantId}/clients/{clientId}/settings
GET   api/tenants/{tenantId}/clients/{clientId}/settings/limits
PATCH api/tenants/{tenantId}/clients/{clientId}/settings
GET   api/tenants/{tenantId}/clients/{clientId}/effective-settings

GET    api/tenants/{tenantId}/clients/{clientId}/secrets
POST   api/tenants/{tenantId}/clients/{clientId}/secrets
GET    api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}
GET    api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/public-key
PUT    api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}
DELETE api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}

GET   api/tenants/{tenantId}/incoherent-clients

*/

/// <summary>
/// Provides the API endpoints for managing an OpenID Client, including its settings and secrets.
/// </summary>
internal class ClientApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IClientValidator clientValidator,
    ISecretValidator secretValidator,
    ISecretGenerator secretGenerator,
    IClientKeyImporter clientKeyImporter,
    ISecretPublicKeyConverter secretPublicKeyConverter,
    ISecretProtectorProvider secretProtectorProvider,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    IClientCoherenceReporter clientCoherenceReporter,
    IClientSecretIssuer clientSecretIssuer,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    IHostManagedClientChecker hostManagedClientChecker,
    IClientKeySourceChecker clientKeySourceChecker,
    ILogger<ClientApiEndpointHandler> logger,
    IAmbientTenantAccessor ambientTenantAccessor
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    // See ADR-1466.
    private const string MachineClientGrantTypesDetail =
        "A machine client obtains tokens only for itself: its 'grant_types_supported' is 'client_credentials' alone.";

    /// <summary>
    /// Gets the entropy, in bits, of the secret that replaces a rotated shared secret.
    /// </summary>
    /// <remarks>
    /// A shared secret stores the UTF-8 length of its base64url string as its key size, and base64url writes four
    /// characters for every three bytes, so <c>storedKeySizeBits / 8 * 3 / 4 * 8</c> recovers the rotated secret's
    /// entropy exactly. The replacement therefore keeps that entropy instead of growing with each rotation, and is
    /// never below <see cref="SharedSecretPolicy.MinimumEntropyBits"/>.
    /// </remarks>
    /// <param name="storedKeySizeBits">The key size, in bits, stored for the rotated shared secret.</param>
    /// <returns>The entropy, in bits, to generate the replacement with.</returns>
    // See ADR-1320.
    internal static int GetReplacementEntropyBits(int storedKeySizeBits) =>
        Math.Max(SharedSecretPolicy.MinimumEntropyBits, storedKeySizeBits / 8 * 3 / 4 * 8);

    /// <summary>
    /// Gets the node above a client, whose authority sets its settings' limits: the node that owns a machine client,
    /// else the client's tenant.
    /// </summary>
    /// <param name="client">The client to get the node above.</param>
    /// <returns>The node above the client.</returns>
    // See ADR-1418 and ADR-1466.
    internal static ResourceNode GetNodeAbove(PersistedClient client) =>
        client.OwningNodeType is not null && client.OwningNodeId is not null
            ? ResourceNode.For(client.TenantId, client.OwningNodeType, client.OwningNodeId)
            : ResourceNode.For(client.TenantId, ResourceNodeTypes.Tenant, client.TenantId);

    /// <summary>
    /// Determines whether a client settings document limits the client to the <c>client_credentials</c> grant alone.
    /// </summary>
    /// <param name="settings">The client's settings document.</param>
    /// <returns>
    /// <see langword="true"/> when <c>grant_types_supported</c> is an array holding exactly the one string
    /// <c>client_credentials</c>; otherwise, <see langword="false"/>.
    /// </returns>
    // See ADR-1466.
    internal static bool IsClientCredentialsOnly(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (
            !settings.TryGetProperty(OpenIdSettingNames.GrantTypesSupported, out var grantTypes)
            || grantTypes.ValueKind != JsonValueKind.Array
            || grantTypes.GetArrayLength() != 1
        )
        {
            return false;
        }

        var grantType = grantTypes[0];
        return grantType.ValueKind == JsonValueKind.String
            && grantType.GetString() == OpenIdProtocol.GrantTypes.ClientCredentials;
    }

    /// <summary>
    /// Returns the earlier of two instants.
    /// </summary>
    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left < right ? left : right;

    /// <summary>
    /// Returns the later of two instants.
    /// </summary>
    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) =>
        left > right ? left : right;

    /// <summary>
    /// Refuses a create request that names an activation or a retirement for a shared secret, which has no lifecycle
    /// of its own: it is active for its whole validity, so an instant it cannot honor is refused rather than dropped.
    /// </summary>
    // See ADR-1403.
    private static ProblemHttpResult? ValidateSharedSecretLifecycle(CreateSecretRequest request) =>
        request.ActivatesWhen is null && request.RetiresWhen is null
            ? null
            : TypedResults.Problem(
                detail: "A shared secret is active for its whole validity: it names no activation or retirement.",
                statusCode: StatusCodes.Status400BadRequest
            );

    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IClientValidator ClientValidator { get; } = clientValidator;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IClientKeyImporter ClientKeyImporter { get; } = clientKeyImporter;
    private ISecretPublicKeyConverter SecretPublicKeyConverter { get; } = secretPublicKeyConverter;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private IClientCoherenceReporter ClientCoherenceReporter { get; } = clientCoherenceReporter;
    private IClientSecretIssuer ClientSecretIssuer { get; } = clientSecretIssuer;

    /// <inheritdoc />
    protected override IManagementAuditRecorder ManagementAuditRecorder { get; } =
        managementAuditRecorder;
    private IHostManagedClientChecker HostManagedClientChecker { get; } = hostManagedClientChecker;
    private IClientKeySourceChecker ClientKeySourceChecker { get; } = clientKeySourceChecker;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<ClientApiEndpointHandler> Logger { get; } = logger;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var clients = endpoints.MapGroup("/clients").WithTags(OpenIdConstants.EndpointTags.Clients);

        clients
            .MapPost("", CreateClientAsync)
            .Produces<ClientResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        clients.MapGet("", ListClientsAsync).Produces<CollectionResource<ClientResource>>();
        clients.MapGet("/{clientId}", GetClientAsync).Produces<ClientResource>();
        clients.MapPatch("/{clientId}", UpdateClientAsync).Produces(StatusCodes.Status204NoContent);
        clients
            .MapDelete("/{clientId}", DeleteClientAsync)
            .Produces(StatusCodes.Status204NoContent);

        clients.MapGet("/{clientId}/settings", GetSettingsAsync).Produces<ClientSettingsResource>();
        clients
            .MapGet("/{clientId}/settings/limits", GetSettingLimitsAsync)
            .Produces<SettingLimitsResource>();
        // The response may reveal a secret the change issues, so no cache may keep it (ADR-1345).
        clients
            .MapPatch("/{clientId}/settings", UpdateSettingsAsync)
            .Produces<CreatedSecretResource>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithNoStore();
        clients
            .MapGet("/{clientId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<ClientEffectiveSettingsResource>();

        clients.MapGet("/{clientId}/secrets", GetSecretsAsync).Produces<ClientSecretsResource>();
        // The response reveals a secret once, so no cache may keep it (ADR-1345).
        clients
            .MapPost("/{clientId}/secrets", CreateSecretAsync)
            .Produces<CreatedSecretResource>(StatusCodes.Status201Created)
            .WithNoStore();
        clients
            .MapPost("/{clientId}/secrets/import", ImportSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        clients.MapGet("/{clientId}/secrets/{secretId}", GetSecretAsync).Produces<SecretResource>();
        clients
            .MapGet("/{clientId}/secrets/{secretId}/public-key", GetPublicKeyAsync)
            .Produces<PublicKeyResource>();
        clients
            .MapPut("/{clientId}/secrets/{secretId}", UpdateSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
        clients
            .MapDelete("/{clientId}/secrets/{secretId}", DeleteSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
        // The response reveals a secret once, so no cache may keep it (ADR-1345).
        clients
            .MapPost("/{clientId}/secrets/{secretId}/rotate", RotateSecretAsync)
            .Produces<CreatedSecretResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithNoStore();

        clients.MapGet("/{clientId}/owners", ListOwnersAsync).Produces<RoleListResource>();
        clients
            .MapPost("/{clientId}/owners", AddOwnerAsync)
            .Produces<RoleHolderResource>(StatusCodes.Status201Created)
            .Produces<RoleHolderResource>(StatusCodes.Status200OK);
        clients
            .MapDelete("/{clientId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent);

        // The report is not under /clients because /clients/{clientId} would answer it as a client identifier.
        endpoints
            .MapGet("/incoherent-clients", ListIncoherentClientsAsync)
            .WithTags(OpenIdConstants.EndpointTags.Clients)
            .Produces<CollectionResource<IncoherentClientResource>>();
    }

    /// <summary>
    /// Maps a <see cref="PersistedClient"/> to its <see cref="ClientResource"/> representation.
    /// </summary>
    /// <param name="client">The <see cref="PersistedClient"/> to map.</param>
    /// <returns>The mapped <see cref="ClientResource"/>.</returns>
    internal virtual ClientResource ToClientResource(PersistedClient client)
    {
        return new ClientResource
        {
            TenantId = client.TenantId,
            ClientId = client.ClientId,
            ConcurrencyToken = client.ConcurrencyToken,
            IsDisabled = client.IsDisabled,
            PrincipalId = client.PrincipalId,
            OwningNodeType = client.OwningNodeType,
            OwningNodeId = client.OwningNodeId,
            IsHostManaged = HostManagedClientChecker.IsHostManaged(
                client.TenantId,
                client.ClientId
            ),
            ResourceServerId = client.ResourceServerId,
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedClientSettings"/> to its <see cref="ClientSettingsResource"/> representation.
    /// </summary>
    /// <param name="settings">The <see cref="PersistedClientSettings"/> to map.</param>
    /// <returns>The mapped <see cref="ClientSettingsResource"/>.</returns>
    internal virtual ClientSettingsResource ToClientSettingsResource(
        PersistedClientSettings settings
    )
    {
        return new ClientSettingsResource
        {
            TenantId = settings.TenantId,
            ClientId = settings.ClientId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };
    }

    /// <summary>
    /// Resolves the addressed tenant's effective settings: the team's effective settings merged with the tenant's
    /// persisted overrides. The tenant's effective view is reconstructed by id, so it is independent of the request's
    /// resolved tenant. Both the team's and the tenant's settings are resolved through the
    /// <see cref="IEffectiveSettingsResolver"/>, so each is expanded with the ceilings of the security profiles it
    /// selects, as it is at runtime.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The tenant's effective <see cref="IReadOnlySettingCollection"/>, or <c>null</c> when the tenant's
    /// settings are absent.</returns>
    /// <exception cref="UnreadableSettingsException">The tenant's document or its team's cannot be honored.</exception>
    // See ADR-1204 and ADR-1245.
    internal virtual async ValueTask<IReadOnlySettingCollection?> MergeTenantSettingsAsync(
        string tenantId,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenant = await storeManager
            .GetStore<ITenantStore>()
            .GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var teamEffective = await EffectiveSettingsResolver.ResolveTeamAsync(
            tenant.TeamId,
            cancellationToken
        );
        return EffectiveSettingsResolver.ResolveStored(
            teamEffective,
            OpenIdSettingScopes.Tenant,
            tenantId,
            tenant.Settings.Value
        );
    }

    /// <summary>
    /// Merges an OpenID Client's persisted settings onto the addressed tenant's effective settings, producing the same
    /// effective <see cref="IReadOnlySettingCollection"/> the authorization server resolves for the client at runtime.
    /// The tenant's effective view is reconstructed by id (see <see cref="MergeTenantSettingsAsync"/>), so the preview
    /// is independent of the request's resolved tenant. The client's settings are resolved through the
    /// <see cref="IEffectiveSettingsResolver"/>, so they are expanded with the ceilings of the security profiles they
    /// select, as they are at runtime. A document that cannot be honored fails naming its owner.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="clientId">The identifier of the OpenID Client that owns the settings document.</param>
    /// <param name="clientSettings">The client's settings document to merge.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The client's effective <see cref="IReadOnlySettingCollection"/>, or <c>null</c> when the owning
    /// tenant's settings are absent.</returns>
    /// <exception cref="UnreadableSettingsException">The client's document, the tenant's or its team's cannot be
    /// honored.</exception>
    // See ADR-1204 and ADR-1245.
    internal virtual async ValueTask<IReadOnlySettingCollection?> MergeClientSettingsAsync(
        string tenantId,
        string clientId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenantEffective = await MergeTenantSettingsAsync(
            tenantId,
            storeManager,
            cancellationToken
        );
        return tenantEffective is null
            ? null
            : EffectiveSettingsResolver.ResolveStored(
                tenantEffective,
                OpenIdSettingScopes.Client,
                clientId,
                clientSettings
            );
    }

    /// <summary>
    /// Verifies that a client's settings document, resolved onto its tenant's effective settings as the runtime
    /// resolves it, satisfies every registered <see cref="IClientCoherenceRule"/> (the algorithms the client registers
    /// for the JWTs addressed to it, the kinds of its redirect URIs, and its token endpoint authentication method). A
    /// mismatch is rejected when the settings are written instead of failing every issuance with
    /// <c>server_error</c> or every authorization request with <c>invalid_request</c>.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="clientSettings">The client's settings document to verify.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances naming each registered setting whose value is not permitted,
    /// or an empty list.</returns>
    // See ADR-1206.
    internal virtual async ValueTask<IReadOnlyList<SettingError>> ValidateEffectiveSettingsAsync(
        string tenantId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenantSettings = await MergeTenantSettingsAsync(
            tenantId,
            storeManager,
            cancellationToken
        );

        return tenantSettings is null
            ? []
            : ClientCoherenceValidator
                .Validate(tenantSettings, clientSettings)
                .Select(violation => new SettingError(violation.SettingName, violation.Description))
                .ToList();
    }

    /// <summary>
    /// Resolves the client-authentication method a client with the specified settings document is registered for,
    /// through the <see cref="IClientSecretIssuer"/>, which stores the client's shared secrets in the form that method
    /// needs.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="clientSettings">The client's settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The client's effective <c>token_endpoint_auth_method</c>, or <see langword="null"/> when the settings
    /// cannot be merged.</returns>
    // See ADR-1391.
    private async ValueTask<string?> GetTokenEndpointAuthMethodAsync(
        string tenantId,
        string clientId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenant = await storeManager
            .GetStore<ITenantStore>()
            .GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        return await ClientSecretIssuer.GetTokenEndpointAuthMethodAsync(
            tenant.TeamId,
            tenantId,
            tenant.Settings.Value,
            clientId,
            clientSettings,
            cancellationToken
        );
    }

    /// <summary>
    /// Writes the client-authentication method a client is registered for into its settings document when the document
    /// names none, so every stored client names its method and a change to its platform's, team's or tenant's
    /// <c>token_endpoint_auth_method</c> never moves the method its shared secrets are stored for.
    /// </summary>
    /// <param name="clientSettings">The client's settings document.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it cannot be resolved.</param>
    /// <returns>The settings document that names the client's method.</returns>
    // See ADR-1391.
    internal virtual JsonElement NameTokenEndpointAuthMethod(
        JsonElement clientSettings,
        string? tokenEndpointAuthMethod
    )
    {
        if (tokenEndpointAuthMethod is null)
        {
            return clientSettings;
        }

        var settings = ToJsonObject(clientSettings);
        if (settings.ContainsKey(OpenIdSettingNames.TokenEndpointAuthMethod))
        {
            return clientSettings;
        }

        settings[OpenIdSettingNames.TokenEndpointAuthMethod] = tokenEndpointAuthMethod;
        return SerializeToElement(settings);
    }

    /// <summary>
    /// Maps an OpenID Client's effective <see cref="IReadOnlySettingCollection"/> to its
    /// <see cref="ClientEffectiveSettingsResource"/> representation.
    /// </summary>
    /// <param name="client">The <see cref="PersistedClient"/> that owns the settings.</param>
    /// <param name="settings">The client's effective <see cref="IReadOnlySettingCollection"/>.</param>
    /// <returns>The mapped <see cref="ClientEffectiveSettingsResource"/>.</returns>
    internal virtual ClientEffectiveSettingsResource ToClientEffectiveSettingsResource(
        PersistedClient client,
        IReadOnlySettingCollection settings
    )
    {
        return new ClientEffectiveSettingsResource
        {
            TenantId = client.TenantId,
            ClientId = client.ClientId,
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.Client
            ),
        };
    }

    /// <summary>
    /// Maps an <see cref="IncoherentClient"/> to its <see cref="IncoherentClientResource"/> representation.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="client">The <see cref="IncoherentClient"/> to map.</param>
    /// <returns>The mapped <see cref="IncoherentClientResource"/>.</returns>
    internal virtual IncoherentClientResource ToIncoherentClientResource(
        string tenantId,
        IncoherentClient client
    )
    {
        return new IncoherentClientResource
        {
            TenantId = tenantId,
            ClientId = client.ClientId,
            Violations =
            [
                .. client.Violations.Select(violation => new ClientCoherenceViolationResource
                {
                    SettingName = violation.SettingName,
                    Value = violation.Value,
                    GoverningSettingName = violation.GoverningSettingName,
                    Description = violation.Description,
                }),
            ],
            DocumentError = client.DocumentError,
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedClientSecrets"/> to its <see cref="ClientSecretsResource"/> representation.
    /// </summary>
    /// <param name="secrets">The <see cref="PersistedClientSecrets"/> to map.</param>
    /// <returns>The mapped <see cref="ClientSecretsResource"/>.</returns>
    internal virtual ClientSecretsResource ToClientSecretsResource(PersistedClientSecrets secrets)
    {
        return new ClientSecretsResource
        {
            TenantId = secrets.TenantId,
            ClientId = secrets.ClientId,
            ConcurrencyToken = secrets.ConcurrencyToken,
            Secrets = ToSecretsResource(secrets.Value),
        };
    }

    /// <summary>
    /// Lists the clients in a tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="resourceServerId">The identifier of a resource server, to list only the clients that act for it; or
    /// <c>null</c> to list every client.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of clients to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of clients.</response>
    [EndpointName("api/clients/list")]
    internal virtual async ValueTask<IResult> ListClientsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? resourceServerId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        // A client list is scoped to the addressed tenant (the query filter enforces it); authorize for that tenant.
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IClientStore>();
                var page = new PageRequest { Cursor = cursor, Limit = effectiveLimit };
                return string.IsNullOrEmpty(resourceServerId)
                    ? await store.GetPageAsync(page, cancellationToken)
                    : await store.GetPageByResourceServerAsync(
                        resourceServerId,
                        page,
                        cancellationToken
                    );
            },
            ToClientResource
        );
    }

    /// <summary>
    /// Lists the machine clients of a team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of clients to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of the machine clients the team owns.</response>
    // See ADR-1466.
    [EndpointName("api/teams/clients/list")]
    internal virtual async ValueTask<IResult> ListTeamClientsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var teamNode = GetTeamNode(httpContext, teamId);
        return await ListMachineClientsAsync(
            httpContext,
            teamNode,
            cursor,
            limit,
            cancellationToken
        );
    }

    /// <summary>
    /// Lists the machine clients of the platform.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of clients to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of the machine clients the platform owns.</response>
    // See ADR-1466.
    [EndpointName("api/platform/clients/list")]
    internal virtual async ValueTask<IResult> ListPlatformClientsAsync(
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var platformNode = GetPlatformNode(httpContext);
        return await ListMachineClientsAsync(
            httpContext,
            platformNode,
            cursor,
            limit,
            cancellationToken
        );
    }

    /// <summary>
    /// Lists the machine clients a node owns, after authorizing the caller at that node.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="owningNode">The platform or team node that owns the machine clients.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of clients to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The result carrying a page of the machine clients the node owns, or the error that refused it.</returns>
    // See ADR-1466.
    internal virtual async ValueTask<IResult> ListMachineClientsAsync(
        HttpContext httpContext,
        ResourceNode owningNode,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            owningNode,
            async () =>
            {
                // Machine clients live in the control-plane tenant, which the node carries.
                using var scope = AmbientTenantAccessor.BeginScope(owningNode.TenantId);
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IClientStore>();
                var page = new PageRequest { Cursor = cursor, Limit = effectiveLimit };
                return await store.GetPageByOwningNodeAsync(
                    owningNode.ResourceType,
                    owningNode.ResourceId,
                    page,
                    cancellationToken
                );
            },
            ToClientResource
        );
    }

    /// <summary>
    /// Lists the clients of a tenant that the tenant's effective settings make incoherent.
    /// </summary>
    /// <remarks>
    /// A page checks up to <paramref name="limit"/> clients of the tenant and lists the incoherent ones among them, so
    /// a page can list none while a continuation token remains. A tenant without settings has no incoherent clients.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of clients to check on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of incoherent clients.</response>
    // See ADR-1233.
    [EndpointName("api/clients/incoherent/list")]
    internal virtual async ValueTask<IResult> ListIncoherentClientsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        // The report is authorized like the client list: for the addressed tenant.
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var tenantSettings = await MergeTenantSettingsAsync(
                    tenantId,
                    storeManager,
                    cancellationToken
                );
                if (tenantSettings is null)
                {
                    return new PagedResult<IncoherentClient> { Items = [], NextCursor = null };
                }

                var page = new PageRequest { Cursor = cursor, Limit = effectiveLimit };
                return await ClientCoherenceReporter.FindIncoherentClientsAsync(
                    storeManager,
                    tenantId,
                    tenantSettings,
                    page,
                    cancellationToken
                );
            },
            client => ToIncoherentClientResource(tenantId, client)
        );
    }

    /// <summary>
    /// Gets a client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested client.</response>
    [EndpointName("api/clients/get")]
    internal virtual async ValueTask<IResult> GetClientAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IClientStore>();
                return await store.GetOrDefaultAsync(clientId, token);
            },
            ToClientResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates a client. The stored settings document names the method the client is registered for, its tenant's
    /// when the request names none.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The <see cref="CreateClientRequest"/> describing the client to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created client.</response>
    [EndpointName("api/clients/create")]
    internal virtual async ValueTask<IResult> CreateClientAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateClientRequest request,
        CancellationToken cancellationToken
    )
    {
        var resourceServerId = string.IsNullOrEmpty(request.ResourceServerId)
            ? null
            : request.ResourceServerId;

        // Authorization is asserted from the route and the request, before any row is read (ADR-1424, ADR-1451).
        var authorizationError = await ClientValidator.AuthorizeCreateAsync(
            httpContext.User,
            tenantId,
            resourceServerId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var clientId = CryptoService.GenerateResourceId();

        var client = new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = request.IsDisabled,
            ResourceServerId = resourceServerId,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = SerializeToElement(ToJsonObject(request.Settings)),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        return await AddClientAsync(httpContext, storeManager, client, request, cancellationToken);
    }

    /// <summary>
    /// Creates a machine client of a team. The client obtains tokens only for itself.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team that owns the client.</param>
    /// <param name="request">The <see cref="CreateClientRequest"/> describing the client to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created machine client.</response>
    // See ADR-1466.
    [EndpointName("api/teams/clients/create")]
    internal virtual async ValueTask<IResult> CreateTeamClientAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] CreateClientRequest request,
        CancellationToken cancellationToken
    )
    {
        var teamNode = GetTeamNode(httpContext, teamId);
        return await CreateMachineClientAsync(httpContext, teamNode, request, cancellationToken);
    }

    /// <summary>
    /// Creates a machine client of the platform. The client obtains tokens only for itself.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="request">The <see cref="CreateClientRequest"/> describing the client to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created machine client.</response>
    // See ADR-1466.
    [EndpointName("api/platform/clients/create")]
    internal virtual async ValueTask<IResult> CreatePlatformClientAsync(
        HttpContext httpContext,
        [FromBody] CreateClientRequest request,
        CancellationToken cancellationToken
    )
    {
        var platformNode = GetPlatformNode(httpContext);
        return await CreateMachineClientAsync(
            httpContext,
            platformNode,
            request,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates a machine client of the node that owns it: a control-plane-tenant client limited to the
    /// <c>client_credentials</c> grant that acts for itself, not for a resource server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="owningNode">The platform or team node that owns the client.</param>
    /// <param name="request">The <see cref="CreateClientRequest"/> describing the client to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <c>201</c> result carrying the created client, or the error that refused it.</returns>
    // See ADR-1466.
    internal virtual async ValueTask<IResult> CreateMachineClientAsync(
        HttpContext httpContext,
        ResourceNode owningNode,
        CreateClientRequest request,
        CancellationToken cancellationToken
    )
    {
        // Authorization is asserted from the route values, before any row is read (ADR-1424, ADR-1451).
        var authorizationError = await ClientValidator.AuthorizeCreateMachineClientAsync(
            httpContext.User,
            owningNode.TenantId,
            owningNode.ResourceType,
            owningNode.ResourceId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        if (!string.IsNullOrEmpty(request.ResourceServerId))
        {
            return TypedResults.Problem(
                detail: "A machine client acts for itself, not for a resource server.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // The node carries the control-plane tenant id, which is where a machine client lives.
        var tenantId = owningNode.TenantId;
        using var scope = AmbientTenantAccessor.BeginScope(tenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        // A team owns its machine clients whether or not it is disabled, as it owns its tenants.
        // See ADR-1466.
        if (owningNode.ResourceType == ResourceNodeTypes.Team)
        {
            var teamStore = storeManager.GetStore<ITeamStore>();
            var team = await teamStore.GetOrDefaultAsync(owningNode.ResourceId, cancellationToken);
            if (team is null)
            {
                return TypedResults.NotFound();
            }
        }

        var clientId = CryptoService.GenerateResourceId();

        var settings = ToJsonObject(request.Settings);
        if (!settings.ContainsKey(OpenIdSettingNames.GrantTypesSupported))
        {
            settings[OpenIdSettingNames.GrantTypesSupported] = new JsonArray(
                OpenIdProtocol.GrantTypes.ClientCredentials
            );
        }

        var settingsValue = SerializeToElement(settings);

        var client = new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = request.IsDisabled,
            ResourceServerId = null,
            OwningNodeType = owningNode.ResourceType,
            OwningNodeId = owningNode.ResourceId,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = settingsValue,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        return await AddClientAsync(httpContext, storeManager, client, request, cancellationToken);
    }

    /// <summary>
    /// Validates and stores a client, and answers the created client. A machine client, which names its owning node,
    /// must be limited to the <c>client_credentials</c> grant and has no creator assignment.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> that stores the client.</param>
    /// <param name="client">The client to store.</param>
    /// <param name="request">The <see cref="CreateClientRequest"/> describing the client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <c>201</c> result carrying the created client, or the error that refused it.</returns>
    private async ValueTask<IResult> AddClientAsync(
        HttpContext httpContext,
        IStoreManager storeManager,
        PersistedClient client,
        CreateClientRequest request,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var store = storeManager.GetStore<IClientStore>();

        // Core preconditions (uniqueness, the resource server) are asserted by the validator (ADR-1014).
        var error = await ClientValidator.ValidateCreateAsync(
            client,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var isMachineClient = client.OwningNodeType is not null;

        // See ADR-1466.
        if (isMachineClient && !IsClientCredentialsOnly(client.Settings.Value))
        {
            var grantTypesError = new SettingError(
                OpenIdSettingNames.GrantTypesSupported,
                MachineClientGrantTypesDetail
            );
            return SettingsValidationProblem([grantTypesError]);
        }

        // Setting names are a closed vocabulary (ADR-1139), and a client carries only the settings that apply within it
        // (ADR-1149): a settings document that cannot be honored is rejected here rather than stored and failed on
        // every request that resolves the client.
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.Client,
            openIdContext.Environment.JsonSerializerOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var effectiveSettingErrors = await ValidateEffectiveSettingsAsync(
            client.TenantId,
            client.Settings.Value,
            storeManager,
            cancellationToken
        );
        if (effectiveSettingErrors.Count > 0)
        {
            return SettingsValidationProblem(effectiveSettingErrors);
        }

        var tokenEndpointAuthMethod = await GetTokenEndpointAuthMethodAsync(
            client.TenantId,
            client.ClientId,
            client.Settings.Value,
            storeManager,
            cancellationToken
        );
        client.Settings.Value = NameTokenEndpointAuthMethod(
            client.Settings.Value,
            tokenEndpointAuthMethod
        );

        await store.AddAsync(client, cancellationToken);

        PersistedRoleAssignment? creatorAssignment = null;
        if (!isMachineClient)
        {
            creatorAssignment = await ResourceOwnershipService.AssignCreatorAsync(
                openIdContext,
                httpContext.User,
                storeManager,
                client.TenantId,
                ResourceNodeTypes.Client,
                client.ClientId,
                cancellationToken
            );
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        var resource = ToClientResource(client);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            client.TenantId,
            client.ClientId,
            ResourceChangeTypes.Created,
            ToResourceValues(resource)
        );

        if (creatorAssignment is not null)
        {
            await RecordRoleAssignmentChangedAsync(
                httpContext,
                creatorAssignment,
                ResourceChangeTypes.Created
            );
        }

        // The store assigns the row token on insert (ADR-1012), so no re-read is needed.
        httpContext.Response.Headers.ETag = client.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{client.TenantId}/clients/{client.ClientId}",
            resource
        );
    }

    /// <summary>
    /// Updates a client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The JSON Patch document to apply to the client metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The client was updated.</response>
    [EndpointName("api/clients/update")]
    internal virtual async ValueTask<IResult> UpdateClientAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] JsonPatchDocument<UpdateClientRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await ClientValidator.AuthorizeUpdateAsync(
            httpContext.User,
            tenantId,
            clientId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var model = new UpdateClientRequest { IsDisabled = client.IsDisabled };

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

        // The core precondition (If-Match) is asserted by the validator against the hydrated
        // model (ADR-1015).
        var error = await ClientValidator.ValidateUpdateAsync(
            client,
            model,
            ifMatch,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
        }

        client.IsDisabled = model.IsDisabled;

        await store.UpdateAsync(client, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            client.TenantId,
            client.ClientId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToClientResource(client))
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The client was deleted.</response>
    [EndpointName("api/clients/delete")]
    internal virtual async ValueTask<IResult> DeleteClientAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await ClientValidator.AuthorizeDeleteAsync(
            httpContext.User,
            tenantId,
            clientId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        // The store removes everything that exists for the client (ADR-1212).
        var error = await ClientValidator.ValidateDeleteAsync(
            client,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
        }

        var resourceValues = ToResourceValues(ToClientResource(client));

        await store.RemoveAsync(clientId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            client.TenantId,
            client.ClientId,
            ResourceChangeTypes.Deleted,
            resourceValues
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a client's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The client's settings.</response>
    [EndpointName("api/clients/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IClientStore>();

                // The client store has no dedicated settings getter; the client is loaded whole (settings included).
                var client = await store.GetOrDefaultAsync(clientId, token);
                return client?.Settings;
            },
            ToClientSettingsResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the limits a client's settings carry and whether the caller may set them: whether it holds update authority
    /// over the client's tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The client's setting limits.</response>
    // See ADR-1448.
    [EndpointName("api/clients/settings/limits/get")]
    internal virtual async ValueTask<IResult> GetSettingLimitsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        async ValueTask<ResourceNode?> ReadNodeAboveAsync(CancellationToken token)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(token);
            var store = storeManager.GetStore<IClientStore>();
            var client = await store.GetOrDefaultAsync(clientId, token);
            return client is null ? null : GetNodeAbove(client);
        }

        var limitNames = SettingSerializer.GetLimits(OpenIdSettingScopes.Client);
        var clientNode = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        return await ProcessGetSettingLimitsAsync(
            httpContext,
            clientNode,
            limitNames,
            ReadNodeAboveAsync,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets a client's effective (resolved) settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The client's effective settings.</response>
    [EndpointName("api/clients/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // A null read is the absent client or the absent tenant, answered 404. See ADR-1245.
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IClientStore>();

                var client = await store.GetOrDefaultAsync(clientId, token);
                if (client is null)
                {
                    return null;
                }

                var effectiveSettings = await MergeClientSettingsAsync(
                    tenantId,
                    clientId,
                    client.Settings.Value,
                    storeManager,
                    token
                );
                return effectiveSettings is null
                    ? null
                    : ToClientEffectiveSettingsResource(client, effectiveSettings);
            },
            resource => resource,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a client's settings. The stored document names the method the client is registered for, its tenant's
    /// when the document names none. When the settings change that method, the client's shared secrets are brought into
    /// the form the method needs, through the <see cref="IClientSecretIssuer"/>: a secret stored as its octets is
    /// replaced by its one-way verifier when the method only compares it, and when the method authenticates with a
    /// shared secret that none of the client's unexpired secrets can serve, a new secret takes their place and is
    /// revealed in this response only. Settings that leave the method as it is leave the secrets as they are.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The client's settings were updated, and the client was issued the secret in the body, whose
    /// material is revealed only here.</response>
    /// <response code="204">The client's settings were updated, and the client keeps a secret it holds.</response>
    [EndpointName("api/clients/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var clientNode = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            clientNode,
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var clientSettings = client.Settings;

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, clientSettings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(clientSettings.Value);

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

        // See ADR-1466.
        if (client.OwningNodeType is not null && !IsClientCredentialsOnly(settingsValue))
        {
            var grantTypesError = new SettingError(
                OpenIdSettingNames.GrantTypesSupported,
                MachineClientGrantTypesDetail
            );
            return SettingsValidationProblem([grantTypesError]);
        }

        var jsonOptions = httpContext.GetOpenIdContext().Environment.JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(
            settingsValue,
            OpenIdSettingScopes.Client,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var changedLimits = SettingSerializer.GetChangedLimits(clientSettings.Value, settingsValue);
        var authorityAbove = GetNodeAbove(client);
        var limitRefusal = await GetLimitChangeRefusalOrDefaultAsync(
            httpContext,
            changedLimits,
            authorityAbove,
            cancellationToken
        );
        if (limitRefusal is not null)
        {
            return limitRefusal;
        }

        if (
            ClientKeySourceChecker.HasJwksUri(settingsValue)
            && ClientKeySourceChecker.HasPublicKeys(client.Secrets.Value)
        )
        {
            return KeySourceConflict();
        }

        var effectiveSettingErrors = await ValidateEffectiveSettingsAsync(
            client.TenantId,
            settingsValue,
            storeManager,
            cancellationToken
        );
        if (effectiveSettingErrors.Count > 0)
        {
            return SettingsValidationProblem(effectiveSettingErrors);
        }

        // The client's shared secrets follow the method the settings register when they change it, through the rule
        // dynamic client registration applies to an update. Settings that leave the method as it is leave the secrets
        // as they are, so an edit never issues a secret to a client that holds none. See ADR-1391.
        var storedTokenEndpointAuthMethod = await GetTokenEndpointAuthMethodAsync(
            client.TenantId,
            client.ClientId,
            clientSettings.Value,
            storeManager,
            cancellationToken
        );
        var tokenEndpointAuthMethod = await GetTokenEndpointAuthMethodAsync(
            client.TenantId,
            client.ClientId,
            settingsValue,
            storeManager,
            cancellationToken
        );
        settingsValue = NameTokenEndpointAuthMethod(settingsValue, tokenEndpointAuthMethod);

        var reconciliation = string.Equals(
            storedTokenEndpointAuthMethod,
            tokenEndpointAuthMethod,
            StringComparison.Ordinal
        )
            ? new ClientSecretReconciliation
            {
                Secrets = [.. client.Secrets.Value],
                RemovedSecrets = [],
                AddedSecrets = [],
            }
            : ClientSecretIssuer.ReconcileClientSecrets(
                await SecretProtectorProvider.GetProtectorAsync(
                    SecretOwner.ForTenant(client.TenantId),
                    [],
                    cancellationToken
                ),
                tokenEndpointAuthMethod,
                client.Secrets.Value
            );

        // Issuing a secret is creating one, which needs the authority CreateSecretAsync needs.
        if (reconciliation.IssuedSecret is not null)
        {
            var createFailure = await GetAuthorizationFailureAsync(
                httpContext,
                clientNode,
                Operations.Create,
                cancellationToken
            );
            if (createFailure is not null)
            {
                return createFailure;
            }
        }

        clientSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(clientSettings, cancellationToken);

        foreach (var secret in reconciliation.RemovedSecrets)
        {
            await store.RemoveSecretAsync(clientId, secret.SecretId, cancellationToken);
        }

        foreach (var secret in reconciliation.AddedSecrets)
        {
            await store.AddSecretAsync(clientId, secret, cancellationToken);
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            clientSettings.TenantId,
            clientSettings.ClientId,
            ResourceChangeTypes.SettingsUpdated,
            ToResourceValues(ToClientSettingsResource(clientSettings))
        );

        await RecordSecretChangesAsync(httpContext, client, reconciliation);

        httpContext.Response.Headers.ETag = clientSettings.ConcurrencyToken;

        // The issued secret's material is revealed here only (ADR-1050).
        return reconciliation.IssuedSecret is { } issued
            ? TypedResults.Ok(ToCreatedSecretResource(issued.Secret, issued.SecretMaterial))
            : TypedResults.NoContent();
    }

    private async ValueTask RecordSecretChangesAsync(
        HttpContext httpContext,
        PersistedClient client,
        ClientSecretReconciliation reconciliation
    )
    {
        foreach (var secret in reconciliation.RemovedSecrets)
        {
            await ManagementAuditRecorder.RecordClientSecretChangedAsync(
                httpContext,
                client.TenantId,
                client.ClientId,
                secret.SecretId,
                ResourceChangeTypes.Deleted,
                ToResourceValues(ToSecretResource(secret))
            );
        }

        foreach (var secret in reconciliation.AddedSecrets)
        {
            await ManagementAuditRecorder.RecordClientSecretChangedAsync(
                httpContext,
                client.TenantId,
                client.ClientId,
                secret.SecretId,
                ResourceChangeTypes.Created,
                ToResourceValues(ToSecretResource(secret))
            );
        }
    }

    /// <summary>
    /// Lists a client's secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The client's secrets.</response>
    [EndpointName("api/clients/secrets/get")]
    internal virtual async ValueTask<IResult> GetSecretsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IClientStore>();
                return await store.GetSecretsOrDefaultAsync(clientId, token);
            },
            ToClientSecretsResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates a client secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The <see cref="CreateSecretRequest"/> describing the secret to generate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created secret.</response>
    [EndpointName("api/clients/secrets/create")]
    internal virtual async ValueTask<IResult> CreateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] CreateSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        var clientNode = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            clientNode,
            Operations.Create,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validation = SecretValidator.ValidateCreate(request, createdWhen);
        if (!validation.IsValid)
        {
            return ToErrorResult(validation.Error);
        }

        if (
            request.SecretType == SecretTypes.Symmetric
            && request.KeySizeBits < SharedSecretPolicy.MinimumEntropyBits
        )
        {
            return TypedResults.Problem(
                detail: $"A client secret must have at least {SharedSecretPolicy.MinimumEntropyBits} bits of entropy.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (
            ClientKeySourceChecker.HasJwksUri(client.Settings.Value)
            && ClientKeySourceChecker.IsPublicKeyType(request.SecretType)
        )
        {
            return KeySourceConflict();
        }

        var isSharedSecret = request.SecretType == SecretTypes.Symmetric;
        if (isSharedSecret && ValidateSharedSecretLifecycle(request) is { } lifecycleError)
        {
            return lifecycleError;
        }

        var tokenEndpointAuthMethod = isSharedSecret
            ? await GetTokenEndpointAuthMethodAsync(
                client.TenantId,
                client.ClientId,
                client.Settings.Value,
                storeManager,
                cancellationToken
            )
            : null;

        var secretId = CryptoService.GenerateResourceId();

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(client.TenantId),
            [],
            cancellationToken
        );

        GeneratedSecret generatedSecret;
        try
        {
            // A client's symmetric secret is its client_secret, presented verbatim for client_secret_basic/post and
            // used as the HMAC key for client_secret_jwt: the issuer stores it in the form the client's method needs,
            // never as the raw-byte / base64url-reveal crypto-key material GenerateSecret produces (which never UTF-8
            // round-trips). See ADR-1110 and ADR-1391.
            generatedSecret = isSharedSecret
                ? ClientSecretIssuer.GenerateClientSecret(
                    protector,
                    tokenEndpointAuthMethod,
                    new SharedSecretRequest
                    {
                        SecretId = secretId,
                        Use = request.Use,
                        Algorithm = request.Algorithm,
                        CreatedWhen = createdWhen,
                        ExpiresWhen = request.ExpiresWhen,
                        EntropyBits = validation.GenerationParameters.KeySizeBits,
                    }
                )
                : SecretGenerator.GenerateSecret(
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
            await store.AddSecretAsync(clientId, generatedSecret.Secret, cancellationToken);
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

        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            client.TenantId,
            clientId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(secretResource)
        );

        // The store assigns the secret's token on insert (ADR-1012), so no re-read is needed.
        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;

        // The one-time material is revealed here only (ADR-1050); the audit snapshot above stays material-free.
        return TypedResults.Created(
            $"/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}",
            ToCreatedSecretResource(generatedSecret.Secret, generatedSecret.SecretMaterial)
        );
    }

    /// <summary>
    /// Imports a caller-supplied public key (JWK, PEM, or certificate) as a new client secret.
    /// </summary>
    /// <remarks>
    /// The key is imported under the client-key policy: it is a public key of the requested type whose declared
    /// <c>kid</c>, <c>use</c>, and <c>alg</c> agree with the key and with the request. A key supplied as a JSON Web Key
    /// keeps its <c>kid</c>; any other key is identified by its secret identifier. The <c>kid</c> must be unique among
    /// the client's keys, and a client that publishes its keys at a <c>jwks_uri</c> cannot have keys registered for it.
    /// </remarks>
    // See ADR-1104 and ADR-1161.
    internal virtual async ValueTask<IResult> ImportSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] ImportSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        var clientNode = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            clientNode,
            Operations.Create,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validationError = SecretValidator.ValidateImport(request, createdWhen);
        if (validationError is not null)
        {
            return ToErrorResult(validationError);
        }

        if (ClientKeySourceChecker.HasJwksUri(client.Settings.Value))
        {
            return KeySourceConflict();
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
        if (!ClientKeyImporter.TryImportKey(importRequest, out var importedSecret, out var error))
        {
            return TypedResults.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        var keyIdConflict = KeyIdConflictOrDefault(client.Secrets.Value, importedSecret, "client");
        if (keyIdConflict is not null)
        {
            return keyIdConflict;
        }

        try
        {
            await store.AddSecretAsync(clientId, importedSecret, cancellationToken);
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

        var secretResource = ToSecretResource(importedSecret);

        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            client.TenantId,
            clientId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(secretResource)
        );

        httpContext.Response.Headers.ETag = importedSecret.ConcurrencyToken;

        // An imported public key has no secret to reveal; return the material-free resource.
        return TypedResults.Created(
            $"/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}",
            secretResource
        );
    }

    /// <summary>
    /// Gets a client secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested secret.</response>
    [EndpointName("api/clients/secrets/get-one")]
    internal virtual async ValueTask<IResult> GetSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IClientStore>();

                var clientSecret = await store.GetSecretOrDefaultAsync(clientId, secretId, token);
                return clientSecret?.Value;
            },
            ToSecretResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the public key material of an asymmetric client secret, as a JWK and a PEM.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The public key material of the requested secret.</response>
    [EndpointName("api/clients/secrets/get-public-key")]
    internal virtual async ValueTask<IResult> GetPublicKeyAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IClientStore>();

                var clientSecret = await store.GetSecretOrDefaultAsync(clientId, secretId, token);
                return clientSecret?.Value;
            },
            // The public key is read from its stored public half, so no master key is loaded (ADR-1268).
            SecretPublicKeyConverter.Convert,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a client secret's metadata.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> with the new metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was updated.</response>
    [EndpointName("api/clients/secrets/update")]
    internal virtual async ValueTask<IResult> UpdateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromRoute] string secretId,
        [FromBody] UpdateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var clientNode = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            clientNode,
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var clientSecret = await store.GetSecretOrDefaultAsync(
            clientId,
            secretId,
            cancellationToken
        );
        if (clientSecret is null)
        {
            return TypedResults.NotFound();
        }

        var secret = clientSecret.Value;

        var client = await GetClientOrDefaultAsync(storeManager, clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
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

        await store.UpdateSecretAsync(clientId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            clientSecret.TenantId,
            clientId,
            secretId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToSecretResource(updated))
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a client secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was deleted.</response>
    [EndpointName("api/clients/secrets/delete")]
    internal virtual async ValueTask<IResult> DeleteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        var clientNode = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            clientNode,
            Operations.Delete,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var clientSecret = await store.GetSecretOrDefaultAsync(
            clientId,
            secretId,
            cancellationToken
        );
        if (clientSecret is null)
        {
            return TypedResults.NotFound();
        }

        var client = await GetClientOrDefaultAsync(storeManager, clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
        }

        var secretValues = ToResourceValues(ToSecretResource(clientSecret.Value));

        await store.RemoveSecretAsync(clientId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            clientSecret.TenantId,
            clientId,
            secretId,
            ResourceChangeTypes.Deleted,
            secretValues
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Rotates a client secret: generates a replacement of the same type, use, and algorithm and adds it beside the
    /// rotated secret.
    /// </summary>
    /// <remarks>
    /// Only a client's shared secret is rotated; a key is the client's to hold and is replaced by importing a new one.
    /// Both secrets are accepted until the rotated one expires or is deleted, so its holder can switch without an
    /// outage; the optional <see cref="RotateSecretRequest.RotatedExpiresWhen"/> bounds that overlap by shortening the
    /// rotated secret's expiry, never extending it. Rotation is authorized as the create and the update it performs.
    /// The replacement's material is revealed in the response only.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret to rotate.</param>
    /// <param name="request">The <see cref="RotateSecretRequest"/> describing the replacement.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the rotated secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The replacement secret.</response>
    // See ADR-1320.
    [EndpointName("api/clients/secrets/rotate")]
    internal virtual async ValueTask<IResult> RotateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromRoute] string secretId,
        [FromBody] RotateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var clientNode = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        var createFailure = await GetAuthorizationFailureAsync(
            httpContext,
            clientNode,
            Operations.Create,
            cancellationToken
        );
        if (createFailure is not null)
        {
            return createFailure;
        }

        var updateFailure = await GetAuthorizationFailureAsync(
            httpContext,
            clientNode,
            Operations.Update,
            cancellationToken
        );
        if (updateFailure is not null)
        {
            return updateFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        var secret = client?.Secrets.Value.FirstOrDefault(candidate =>
            string.Equals(candidate.SecretId, secretId, StringComparison.Ordinal)
        );
        if (client is null || secret is null)
        {
            return TypedResults.NotFound();
        }

        var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
            httpContext,
            client,
            cancellationToken
        );
        if (changeRefusal is not null)
        {
            return changeRefusal;
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, secret.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        if (secret.SecretType != SecretTypes.Symmetric)
        {
            return TypedResults.Problem(
                detail: "Only a client's shared secret can be rotated; a key is replaced by importing a new one and "
                    + "deleting the old one.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var createRequest = new CreateSecretRequest
        {
            SecretType = secret.SecretType,
            KeySizeBits = GetReplacementEntropyBits(secret.KeySizeBits),
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            ExpiresWhen = request.ExpiresWhen,
        };
        var createdWhen = TimeProvider.GetUtcNow();
        var validation = SecretValidator.ValidateCreate(createRequest, createdWhen);
        if (!validation.IsValid)
        {
            return ToErrorResult(validation.Error);
        }

        // The replacement's form follows the method the client is registered for, whatever the rotated secret's form.
        var tokenEndpointAuthMethod = await GetTokenEndpointAuthMethodAsync(
            client.TenantId,
            client.ClientId,
            client.Settings.Value,
            storeManager,
            cancellationToken
        );

        var replacementId = CryptoService.GenerateResourceId();

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(client.TenantId),
            [],
            cancellationToken
        );

        GeneratedSecret generatedSecret;
        try
        {
            // A client's shared secret is its client_secret (ADR-1110), stored in the form the client's method needs
            // (ADR-1391).
            generatedSecret = ClientSecretIssuer.GenerateClientSecret(
                protector,
                tokenEndpointAuthMethod,
                new SharedSecretRequest
                {
                    SecretId = replacementId,
                    Use = createRequest.Use,
                    Algorithm = createRequest.Algorithm,
                    CreatedWhen = createdWhen,
                    ExpiresWhen = createRequest.ExpiresWhen,
                    EntropyBits = validation.GenerationParameters.KeySizeBits,
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

        // The replacement is added beside the rotated secret in one unit of work; the rotated secret keeps
        // authenticating until its (possibly shortened) expiry passes or it is deleted (ADR-1320). A requested expiry
        // in the past stops it at once; one before its creation stops it at its creation, the earliest instant its
        // lifecycle can hold (ADR-1403).
        var rotatedExpiresWhen =
            request.RotatedExpiresWhen is { } requested && requested < secret.ExpiresWhen
                ? Max(requested, secret.CreatedWhen)
                : secret.ExpiresWhen;

        try
        {
            await store.AddSecretAsync(clientId, generatedSecret.Secret, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: "A secret with the specified identifier already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        PersistedSecret? shortened = null;
        if (rotatedExpiresWhen != secret.ExpiresWhen)
        {
            // The shortened expiry bounds the secret's active window with it: a secret stopped at once neither signs
            // nor authenticates from that instant, whatever its activation and retirement were.
            var shortenedMetadata = new UpdateSecretRequest
            {
                Use = secret.Use,
                Algorithm = secret.Algorithm,
                ExpiresWhen = rotatedExpiresWhen,
                ActivatesWhen = Min(secret.ActivatesWhen, rotatedExpiresWhen),
                RetiresWhen = Min(secret.RetiresWhen, rotatedExpiresWhen),
            };
            shortened = WithUpdate(secret, shortenedMetadata);
            await store.UpdateSecretAsync(clientId, shortened, cancellationToken);
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        var replacementResource = ToSecretResource(generatedSecret.Secret);

        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            client.TenantId,
            clientId,
            replacementId,
            ResourceChangeTypes.Created,
            ToResourceValues(replacementResource)
        );
        if (shortened is not null)
        {
            var shortenedResource = ToSecretResource(shortened);
            var shortenedValues = ToResourceValues(shortenedResource);
            await ManagementAuditRecorder.RecordClientSecretChangedAsync(
                httpContext,
                client.TenantId,
                clientId,
                secretId,
                ResourceChangeTypes.Updated,
                shortenedValues
            );
        }

        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;

        // The replacement's material is revealed here only (ADR-1050).
        return TypedResults.Created(
            $"/tenants/{tenantId}/clients/{clientId}/secrets/{replacementId}",
            ToCreatedSecretResource(generatedSecret.Secret, generatedSecret.SecretMaterial)
        );
    }

    /// <summary>
    /// Lists a client's owners and whether the caller may grant and revoke the role.
    /// </summary>
    /// <response code="200">The client's owners and whether the caller may change them.</response>
    [EndpointName("api/clients/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        // See ADR-1465.
        var roleList = GetOwnerList(tenantId, clientId);

        async ValueTask<bool> CanChangeOwnersAsync(
            IStoreManager storeManager,
            CancellationToken token
        )
        {
            if (!await HoldsAuthorityAsync(httpContext, roleList.Node, roleList.Operation))
            {
                return false;
            }

            var client = await GetClientOrDefaultAsync(storeManager, clientId, token);
            if (client is null)
            {
                return false;
            }

            var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
                httpContext,
                client,
                token
            );
            return changeRefusal is null;
        }

        return await ProcessListRoleAssignmentsAsync(
            httpContext,
            roleList,
            CanChangeOwnersAsync,
            cancellationToken
        );
    }

    /// <summary>
    /// Adds an owner to a client.
    /// </summary>
    /// <response code="201">The owner was added.</response>
    /// <response code="200">The principal already owns the client.</response>
    [EndpointName("api/clients/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] AddRoleHolderRequest request,
        CancellationToken cancellationToken
    )
    {
        // See ADR-1469.
        var roleList = GetOwnerList(tenantId, clientId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            roleList.Node,
            roleList.Operation
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var client = await GetClientOrDefaultAsync(storeManager, clientId, cancellationToken);
        if (client is not null)
        {
            var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
                httpContext,
                client,
                cancellationToken
            );
            if (changeRefusal is not null)
            {
                return changeRefusal;
            }
        }

        return await ProcessAddRoleAssignmentAsync(
            httpContext,
            roleList,
            request,
            cancellationToken
        );
    }

    /// <summary>
    /// Removes an owner from a client.
    /// </summary>
    /// <response code="204">The owner was removed.</response>
    [EndpointName("api/clients/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        var roleList = GetOwnerList(tenantId, clientId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            roleList.Node,
            roleList.Operation
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var client = await GetClientOrDefaultAsync(storeManager, clientId, cancellationToken);
        if (client is not null)
        {
            var changeRefusal = await GetClientChangeRefusalOrDefaultAsync(
                httpContext,
                client,
                cancellationToken
            );
            if (changeRefusal is not null)
            {
                return changeRefusal;
            }
        }

        return await ProcessRemoveOwnerAsync(httpContext, roleList, principalId, cancellationToken);
    }

    // Describes the owners of a client: the role is granted and revoked with 'Update' at the client's node.
    private static RoleList GetOwnerList(string tenantId, string clientId) =>
        new()
        {
            Node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId),
            RoleName = BuiltInRoles.Owner,
            Operation = Operations.Update,
            Path = $"/tenants/{tenantId}/clients/{clientId}/owners",
            ResourceExistsAsync = (storeManager, token) =>
                ClientExistsAsync(storeManager, clientId, token),
        };

    /// <summary>
    /// Gets the refusal of a change to a client: the <c>409 Conflict</c> for a client the host configures, else the
    /// <c>403 Forbidden</c> when the client is a machine client and the caller lacks creation authority at the node
    /// that owns it, else <c>null</c> when the change may proceed.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="client">The client the request changes.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The refusal result, or <c>null</c> when the change may proceed.</returns>
    // See ADR-1417 and ADR-1467.
    private async ValueTask<IResult?> GetClientChangeRefusalOrDefaultAsync(
        HttpContext httpContext,
        PersistedClient client,
        CancellationToken cancellationToken
    )
    {
        if (HostManagedClientChecker.IsHostManaged(client.TenantId, client.ClientId))
        {
            return HostManagedClientRefused();
        }

        if (client.OwningNodeType is null)
        {
            return null;
        }

        var owningNode = GetNodeAbove(client);
        return await GetAuthorizationFailureAsync(
            httpContext,
            owningNode,
            Operations.Create,
            cancellationToken
        );
    }

    /// <summary>
    /// Loads a client, the one lookup behind every request that changes a client through one of its secrets or owners.
    /// </summary>
    /// <param name="storeManager">The <see cref="IStoreManager"/> that reads the client.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The client, or <c>null</c> when it does not exist.</returns>
    private static async ValueTask<PersistedClient?> GetClientOrDefaultAsync(
        IStoreManager storeManager,
        string clientId,
        CancellationToken cancellationToken
    ) => await storeManager.GetStore<IClientStore>().GetOrDefaultAsync(clientId, cancellationToken);

    private static async ValueTask<bool> ClientExistsAsync(
        IStoreManager storeManager,
        string clientId,
        CancellationToken cancellationToken
    ) =>
        await storeManager.GetStore<IClientStore>().GetOrDefaultAsync(clientId, cancellationToken)
            is not null;

    // A client's public keys come from one source, the keys registered for it or the keys it publishes at its
    // jwks_uri (OpenID Connect Dynamic Client Registration 1.0 §2). See ADR-1161.
    private static ProblemHttpResult KeySourceConflict() =>
        TypedResults.Problem(
            detail: "A client's public keys come from one source: the keys registered for it or the keys it "
                + "publishes at its 'jwks_uri', never both.",
            statusCode: StatusCodes.Status409Conflict
        );

    // A client the host configures carries authority from that configuration, so no management caller changes it.
    // See ADR-1417.
    private static ProblemHttpResult HostManagedClientRefused() =>
        TypedResults.Problem(
            detail: "The host configures this client, so the management API does not change it.",
            statusCode: StatusCodes.Status409Conflict
        );
}
