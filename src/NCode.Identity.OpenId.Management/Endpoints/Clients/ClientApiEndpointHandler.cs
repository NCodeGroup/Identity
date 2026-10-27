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

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Json;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/*

GET   api/tenants/{tenantId}/clients/{clientId}

GET   api/tenants/{tenantId}/clients/{clientId}/settings
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
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretPublicKeyConverter secretPublicKeyConverter,
    ISecretProtectorProvider secretProtectorProvider,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    IClientCoherenceReporter clientCoherenceReporter,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<ClientApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IClientValidator ClientValidator { get; } = clientValidator;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IClientKeyImporter ClientKeyImporter { get; } = clientKeyImporter;
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
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
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<ClientApiEndpointHandler> Logger { get; } = logger;

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
            .MapPatch("/{clientId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        clients
            .MapGet("/{clientId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<ClientEffectiveSettingsResource>();

        clients.MapGet("/{clientId}/secrets", GetSecretsAsync).Produces<ClientSecretsResource>();
        clients
            .MapPost("/{clientId}/secrets", CreateSecretAsync)
            .Produces<CreatedSecretResource>(StatusCodes.Status201Created);
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
        clients
            .MapPost("/{clientId}/secrets/{secretId}/rotate", RotateSecretAsync)
            .Produces<CreatedSecretResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        clients
            .MapGet("/{clientId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        clients
            .MapPost("/{clientId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
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
    /// Resolves the addressed tenant's effective settings: the server's committed settings merged with the tenant's
    /// persisted overrides. The tenant's effective view is reconstructed by id, so it is independent of the request's
    /// resolved tenant. Both the server's and the tenant's settings are resolved through the
    /// <see cref="IEffectiveSettingsResolver"/>, so each is expanded with the ceilings of the security profiles it
    /// selects, as it is at runtime.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The tenant's effective <see cref="IReadOnlySettingCollection"/>, or <c>null</c> when the tenant's
    /// settings are absent.</returns>
    /// <exception cref="UnreadableSettingsException">The tenant's document or the server's cannot be honored.</exception>
    // See ADR-0204 and ADR-0245.
    internal virtual async ValueTask<IReadOnlySettingCollection?> MergeTenantSettingsAsync(
        string tenantId,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenantSettings = await storeManager
            .GetStore<ITenantStore>()
            .GetSettingsOrDefaultAsync(tenantId, cancellationToken);
        if (tenantSettings is null)
        {
            return null;
        }

        var serverEffective = await EffectiveSettingsResolver.ResolveServerAsync(cancellationToken);
        return EffectiveSettingsResolver.ResolveStored(
            serverEffective,
            OpenIdSettingScopes.Tenant,
            tenantId,
            tenantSettings.Value
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
    /// <returns>The client's effective <see cref="IReadOnlySettingCollection"/>, or <c>null</c> when the owning tenant's
    /// settings are absent.</returns>
    /// <exception cref="UnreadableSettingsException">The client's document, the tenant's or the server's cannot be
    /// honored.</exception>
    // See ADR-0204 and ADR-0245.
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
    /// <returns>The <see cref="SettingError"/> instances naming each registered setting whose value is not permitted, or an
    /// empty list.</returns>
    // See ADR-0206.
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
    // See ADR-0233.
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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(httpContext, client, node, Operations.Read, ToClientResource);
    }

    /// <summary>
    /// Creates a client.
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
        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var clientId = CryptoService.GenerateResourceId();

        var client = new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = request.IsDisabled,
            ResourceServerId = string.IsNullOrEmpty(request.ResourceServerId)
                ? null
                : request.ResourceServerId,
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

        // Core preconditions (authorization + uniqueness) are asserted by the validator (ADR-0014).
        var error = await ClientValidator.ValidateCreateAsync(
            httpContext.User,
            client,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // Setting names are a closed vocabulary (ADR-0139), and a client carries only the settings that apply within it
        // (ADR-0149): a settings document that cannot be honored is rejected here rather than stored and failed on every
        // request that resolves the client.
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
            tenantId,
            client.Settings.Value,
            storeManager,
            cancellationToken
        );
        if (effectiveSettingErrors.Count > 0)
        {
            return SettingsValidationProblem(effectiveSettingErrors);
        }

        await store.AddAsync(client, cancellationToken);
        await ResourceOwnershipService.AssignCreatorAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            client.TenantId,
            ResourceNodeTypes.Client,
            client.ClientId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        var resource = ToClientResource(client);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            client.TenantId,
            client.ClientId,
            ResourceChangeTypes.Created,
            ToResourceValues(resource),
            cancellationToken
        );

        // The store assigns the row token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = client.ConcurrencyToken;
        return TypedResults.Created($"/tenants/{tenantId}/clients/{client.ClientId}", resource);
    }

    /// <summary>
    /// Updates a client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The JSON Patch document to apply to the client metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The client was updated.</response>
    [EndpointName("api/clients/update")]
    internal virtual async ValueTask<IResult> UpdateClientAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        [FromBody] JsonPatchDocument<UpdateClientRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
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

        // Core preconditions (authorization + If-Match) are asserted by the validator against the hydrated
        // model (ADR-0015).
        var error = await ClientValidator.ValidateUpdateAsync(
            httpContext.User,
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

        client.IsDisabled = model.IsDisabled;

        await store.UpdateAsync(client, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            client.TenantId,
            client.ClientId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToClientResource(client)),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The client was deleted.</response>
    [EndpointName("api/clients/delete")]
    internal virtual async ValueTask<IResult> DeleteClientAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        // The core precondition (authorization) is asserted by the validator (ADR-0014); the store removes everything that exists for the client (ADR-0212).
        var error = await ClientValidator.ValidateDeleteAsync(
            httpContext.User,
            client,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var resourceValues = ToResourceValues(ToClientResource(client));

        await store.RemoveAsync(clientId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            client.TenantId,
            client.ClientId,
            ResourceChangeTypes.Deleted,
            resourceValues,
            cancellationToken
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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        // The client store has no dedicated settings getter; the client is loaded whole (settings included).
        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            client?.Settings,
            node,
            Operations.Read,
            ToClientSettingsResource
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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);

        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // A null projection is the absent tenant, answered 404. See ADR-0245.
        return await ProcessGetAsync(
            httpContext,
            client.Settings.Value,
            node,
            Operations.Read,
            async (settings, token) =>
            {
                var effectiveSettings = await MergeClientSettingsAsync(
                    tenantId,
                    clientId,
                    settings,
                    storeManager,
                    token
                );
                return effectiveSettings is null
                    ? null
                    : ToClientEffectiveSettingsResource(client, effectiveSettings);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a client's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The client's settings were updated.</response>
    [EndpointName("api/clients/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var clientSettings = client.Settings;

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            clientSettings,
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
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
        var jsonOptions = httpContext.GetOpenIdContext().Environment.JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettingsUpdate(
            clientSettings.Value,
            settingsValue,
            OpenIdSettingScopes.Client,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        if (HasJwksUri(settingsValue) && HasPublicKeys(client.Secrets.Value))
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

        clientSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(clientSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientChangedAsync(
            httpContext,
            clientSettings.TenantId,
            clientSettings.ClientId,
            ResourceChangeTypes.SettingsUpdated,
            ToResourceValues(ToClientSettingsResource(clientSettings)),
            cancellationToken
        );

        httpContext.Response.Headers.ETag = clientSettings.ConcurrencyToken;

        return TypedResults.NoContent();
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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var clientSecrets = await store.GetSecretsOrDefaultAsync(clientId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            clientSecrets,
            node,
            Operations.Read,
            ToClientSecretsResource
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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            client,
            Operations.Create
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var validationError = SecretValidator.ValidateCreate(request);
        if (validationError is not null)
        {
            return ToErrorResult(validationError);
        }

        if (HasJwksUri(client.Settings.Value) && IsPublicKeyType(request.SecretType))
        {
            return KeySourceConflict();
        }

        var secretId = CryptoService.GenerateResourceId();

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(client.TenantId),
            [],
            cancellationToken
        );

        GeneratedSecret generatedSecret;
        try
        {
            var generateRequest = new GenerateSecretRequest
            {
                SecretId = secretId,
                SecretType = request.SecretType,
                KeySizeBits = request.KeySizeBits,
                Curve = request.Curve,
                Use = request.Use,
                Algorithm = request.Algorithm,
                CreatedWhen = TimeProvider.GetUtcNow(),
                ExpiresWhen = request.ExpiresWhen,
            };

            // A client's symmetric secret is its client_secret — presented verbatim for client_secret_basic/post and
            // used as the HMAC key for client_secret_jwt — so it must be stored as the UTF-8 bytes of the opaque
            // string (GenerateSharedSecret), not the raw-byte / base64url-reveal crypto-key material GenerateSecret
            // produces for asymmetric JWKS keys (which never UTF-8 round-trips). See ADR-0110.
            generatedSecret =
                request.SecretType == SecretTypes.Symmetric
                    ? SecretGenerator.GenerateSharedSecret(protector, generateRequest)
                    : SecretGenerator.GenerateSecret(protector, generateRequest);
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
            ToResourceValues(secretResource),
            cancellationToken
        );

        // The store assigns the secret's token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;

        // The one-time material is revealed here only (ADR-0050); the audit snapshot above stays material-free.
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
    // See ADR-0104 and ADR-0161.
    internal virtual async ValueTask<IResult> ImportSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] ImportSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            client,
            Operations.Create
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var validationError = SecretValidator.ValidateImport(request);
        if (validationError is not null)
        {
            return ToErrorResult(validationError);
        }

        if (HasJwksUri(client.Settings.Value))
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
            CreatedWhen = TimeProvider.GetUtcNow(),
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
            ToResourceValues(secretResource),
            cancellationToken
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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        // The discrete getter resolves the secret together with its owning TenantId, which scopes authorization.
        var clientSecret = await store.GetSecretOrDefaultAsync(
            clientId,
            secretId,
            cancellationToken
        );

        var scoped = clientSecret is null
            ? null
            : TenantOwnedResource.For(clientSecret.TenantId, clientSecret.Value);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            scoped,
            node,
            Operations.Read,
            x => ToSecretResource(x.Value)
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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        // The discrete getter resolves the secret together with its owning TenantId, which scopes authorization.
        var clientSecret = await store.GetSecretOrDefaultAsync(
            clientId,
            secretId,
            cancellationToken
        );

        if (clientSecret is null)
        {
            return TypedResults.NotFound();
        }

        var scoped = TenantOwnedResource.For(clientSecret.TenantId, clientSecret.Value);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            scoped,
            node,
            Operations.Read,
            // The public key is read from its stored public half, so no master key is loaded (ADR-0268).
            x => SecretPublicKeyConverter.Convert(x.Value)
        );
    }

    /// <summary>
    /// Updates a client secret's metadata.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> with the new metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was updated.</response>
    [EndpointName("api/clients/secrets/update")]
    internal virtual async ValueTask<IResult> UpdateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        [FromRoute] string secretId,
        [FromBody] UpdateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
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

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(clientSecret.TenantId, secret),
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

        var updated = new PersistedSecret
        {
            SecretId = secret.SecretId,
            KeyId = secret.KeyId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = secret.SecretType,
            SecretEncoding = secret.SecretEncoding,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
            PublicKey = secret.PublicKey,
        };

        await store.UpdateSecretAsync(clientId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            clientSecret.TenantId,
            clientId,
            secretId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToSecretResource(updated)),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a client secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was deleted.</response>
    [EndpointName("api/clients/secrets/delete")]
    internal virtual async ValueTask<IResult> DeleteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
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

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(clientSecret.TenantId, clientSecret.Value),
            Operations.Delete
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
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
            secretValues,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Rotates a client secret: generates a replacement of the same type, size, use, and algorithm, and removes the
    /// rotated secret in the same change.
    /// </summary>
    /// <remarks>
    /// Only a client's shared secret is rotated; a key is the client's to hold and is replaced by importing a new one.
    /// The rotated secret stops being accepted at once, so a client that needs both to overlap creates the replacement
    /// and deletes the rotated secret in two calls. Rotation is authorized as those two calls are. The replacement's
    /// material is revealed in the response only.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret to rotate.</param>
    /// <param name="request">The <see cref="RotateSecretRequest"/> describing the replacement.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the rotated secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The replacement secret.</response>
    // See ADR-0183.
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

        var createResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            client,
            Operations.Create
        );
        if (!createResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var deleteResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(client.TenantId, secret),
            Operations.Delete
        );
        if (!deleteResult.Succeeded)
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
            KeySizeBits = secret.KeySizeBits,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            ExpiresWhen = request.ExpiresWhen,
        };
        var validationError = SecretValidator.ValidateCreate(createRequest);
        if (validationError is not null)
        {
            return ToErrorResult(validationError);
        }

        var replacementId = CryptoService.GenerateResourceId();

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(client.TenantId),
            [],
            cancellationToken
        );

        GeneratedSecret generatedSecret;
        try
        {
            // A client's shared secret is its client_secret, stored as the UTF-8 bytes of the opaque string (ADR-0110).
            generatedSecret = SecretGenerator.GenerateSharedSecret(
                protector,
                new GenerateSecretRequest
                {
                    SecretId = replacementId,
                    SecretType = createRequest.SecretType,
                    KeySizeBits = createRequest.KeySizeBits,
                    Use = createRequest.Use,
                    Algorithm = createRequest.Algorithm,
                    CreatedWhen = TimeProvider.GetUtcNow(),
                    ExpiresWhen = createRequest.ExpiresWhen,
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

        var rotatedValues = ToResourceValues(ToSecretResource(secret));

        // The replacement is added and the rotated secret removed in one unit of work, so the client is never left
        // with both or with neither.
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

        await store.RemoveSecretAsync(clientId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        var replacementResource = ToSecretResource(generatedSecret.Secret);

        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            client.TenantId,
            clientId,
            replacementId,
            ResourceChangeTypes.Created,
            ToResourceValues(replacementResource),
            cancellationToken
        );
        await ManagementAuditRecorder.RecordClientSecretChangedAsync(
            httpContext,
            client.TenantId,
            clientId,
            secretId,
            ResourceChangeTypes.Deleted,
            rotatedValues,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;

        // The replacement's material is revealed here only (ADR-0050).
        return TypedResults.Created(
            $"/tenants/{tenantId}/clients/{clientId}/secrets/{replacementId}",
            ToCreatedSecretResource(generatedSecret.Secret, generatedSecret.SecretMaterial)
        );
    }

    /// <summary>
    /// Lists a client's owners.
    /// </summary>
    /// <response code="200">A page of the client's owners.</response>
    [EndpointName("api/clients/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessListOwnersAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Client,
            clientId,
            (storeManager, token) => ClientExistsAsync(storeManager, clientId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Adds an owner to a client.
    /// </summary>
    /// <response code="201">The owner was added.</response>
    [EndpointName("api/clients/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    )
    {
        return await ProcessAddOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Client,
            clientId,
            request,
            $"/tenants/{tenantId}/clients/{clientId}/owners",
            (storeManager, token) => ClientExistsAsync(storeManager, clientId, token),
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
        return await ProcessRemoveOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Client,
            clientId,
            principalId,
            cancellationToken
        );
    }

    private static async ValueTask<bool> ClientExistsAsync(
        IStoreManager storeManager,
        string clientId,
        CancellationToken cancellationToken
    ) =>
        await storeManager.GetStore<IClientStore>().GetOrDefaultAsync(clientId, cancellationToken)
            is not null;

    // A client's public keys come from one source, the keys registered for it or the keys it publishes at its
    // jwks_uri (OpenID Connect Dynamic Client Registration 1.0 §2). See ADR-0161.
    private static ProblemHttpResult KeySourceConflict() =>
        TypedResults.Problem(
            detail: "A client's public keys come from one source: the keys registered for it or the keys it "
                + "publishes at its 'jwks_uri', never both.",
            statusCode: StatusCodes.Status409Conflict
        );

    private static bool HasJwksUri(JsonElement settings) =>
        settings.ValueKind == JsonValueKind.Object
        && settings.TryGetPropertyValue<string>(OpenIdSettingNames.JwksUri, out var jwksUri)
        && !string.IsNullOrEmpty(jwksUri);

    private bool IsPublicKeyType(string secretType) =>
        SecretKeyTypeCatalog.TryGet(secretType, out var descriptor) && descriptor.IsAsymmetric;

    private bool HasPublicKeys(IEnumerable<PersistedSecret> secrets) =>
        secrets.Any(secret => IsPublicKeyType(secret.SecretType));
}
