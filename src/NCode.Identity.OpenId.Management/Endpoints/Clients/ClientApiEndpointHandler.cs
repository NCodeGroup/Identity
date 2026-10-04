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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
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
PUT    api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}
DELETE api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}

*/

/// <summary>
/// Provides the API endpoints for managing an OpenID Client, including its settings and secrets.
/// </summary>
internal class ClientApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IClientValidator clientValidator,
    ISecretGenerator secretGenerator,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    IOpenIdServerProvider serverProvider,
    IResourceOwnershipService resourceOwnershipService,
    ILogger<ClientApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IClientValidator ClientValidator { get; } = clientValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private IOpenIdServerProvider ServerProvider { get; } = serverProvider;

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
            .Produces<ClientResource>(StatusCodes.Status201Created);
        clients.MapGet("", ListClientsAsync).Produces<CollectionResource<ClientResource>>();
        clients.MapGet("/{clientId}", GetClientAsync).Produces<ClientResource>();
        clients.MapPatch("/{clientId}", UpdateClientAsync);
        clients.MapDelete("/{clientId}", DeleteClientAsync);

        clients.MapGet("/{clientId}/settings", GetSettingsAsync).Produces<ClientSettingsResource>();
        clients.MapPatch("/{clientId}/settings", UpdateSettingsAsync);
        clients
            .MapGet("/{clientId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<ClientEffectiveSettingsResource>();

        clients.MapGet("/{clientId}/secrets", GetSecretsAsync).Produces<ClientSecretsResource>();
        clients
            .MapPost("/{clientId}/secrets", CreateSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        clients.MapGet("/{clientId}/secrets/{secretId}", GetSecretAsync).Produces<SecretResource>();
        clients.MapPut("/{clientId}/secrets/{secretId}", UpdateSecretAsync);
        clients.MapDelete("/{clientId}/secrets/{secretId}", DeleteSecretAsync);

        clients
            .MapGet("/{clientId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        clients
            .MapPost("/{clientId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        clients.MapDelete("/{clientId}/owners/{principalId}", RemoveOwnerAsync);
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
    /// Merges an OpenID Client's persisted settings onto the addressed tenant's effective settings, producing the same
    /// effective <see cref="IReadOnlySettingCollection"/> the authorization server resolves for the client at runtime.
    /// The tenant's effective view is reconstructed by id (the server baseline merged with the tenant's persisted
    /// overrides), so the preview is independent of the request's resolved tenant (ADR-0042).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="client">The <see cref="PersistedClient"/> whose settings to merge.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The client's effective <see cref="IReadOnlySettingCollection"/>, or <c>null</c> when the owning tenant's
    /// settings are absent.</returns>
    internal virtual async ValueTask<IReadOnlySettingCollection?> MergeClientSettingsAsync(
        OpenIdContext openIdContext,
        string tenantId,
        PersistedClient client,
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

        var jsonOptions = openIdContext.Environment.JsonSerializerOptions;
        var server = await ServerProvider.GetAsync(openIdContext.Environment, cancellationToken);

        var tenantEffective = server.SettingsProvider.Collection.Merge(
            SettingSerializer.DeserializeSettings(tenantSettings.Value, jsonOptions)
        );
        return tenantEffective.Merge(
            SettingSerializer.DeserializeSettings(client.Settings.Value, jsonOptions)
        );
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
            Settings = ToEffectiveSettingsElement(settings),
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
    /// Handles <c>GET api/tenants/{tenantId}/clients</c>, returning a page of OpenID Clients in the addressed tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of clients to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/clients/list")]
    internal virtual async ValueTask<IResult> ListClientsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        // A client list is scoped to the addressed tenant (the query filter enforces it); authorize for that tenant.
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            new TenantScopeResource(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IClientStore>();
                return await store.GetPageAsync(cursor, effectiveLimit, cancellationToken);
            },
            ToClientResource
        );
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/clients/{clientId}</c>, returning the specified OpenID Client resource.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
    /// Handles <c>POST api/tenants/{tenantId}/clients</c>, creating a new OpenID Client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The <see cref="CreateClientRequest"/> describing the client to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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

        // The store assigns the row token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = client.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/clients/{client.ClientId}",
            ToClientResource(client)
        );
    }

    /// <summary>
    /// Handles <c>PATCH api/tenants/{tenantId}/clients/{clientId}</c>, applying a JSON Patch document to a client's metadata.
    /// Honors an <c>If-Match</c> precondition.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The JSON Patch document to apply to the client metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>DELETE api/tenants/{tenantId}/clients/{clientId}</c>, removing a client. The removal is rejected with
    /// <c>409 Conflict</c> while the client still has secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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

        // Core preconditions (authorization + no-dependents) are asserted by the validator (ADR-0014).
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

        await store.RemoveAsync(clientId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/clients/{clientId}/settings</c>, returning the settings for the specified OpenID Client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
    /// Handles <c>GET api/tenants/{tenantId}/clients/{clientId}/effective-settings</c>, returning the resolved, merged settings view for
    /// the specified OpenID Client — the client's persisted settings merged onto the addressed tenant's effective
    /// settings through the settings merge pipeline, including settings that are not advertised in discovery.
    /// Authorization requires the same <c>Read</c> permission on the client as reading its persisted settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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

        var openIdContext = httpContext.GetOpenIdContext();
        var effectiveSettings = await MergeClientSettingsAsync(
            openIdContext,
            tenantId,
            client,
            storeManager,
            cancellationToken
        );
        if (effectiveSettings is null)
        {
            return TypedResults.NotFound();
        }

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId);
        return await ProcessGetAsync(
            httpContext,
            effectiveSettings,
            node,
            Operations.Read,
            settings => ToClientEffectiveSettingsResource(client, settings)
        );
    }

    /// <summary>
    /// Handles <c>PATCH api/tenants/{tenantId}/clients/{clientId}/settings</c>, applying a JSON Patch document to the settings for the
    /// specified OpenID Client. Honors an <c>If-Match</c> precondition and returns the refreshed <c>ETag</c>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
        var clientSettings = client?.Settings;

        if (clientSettings is null)
        {
            return TypedResults.NotFound();
        }

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

        clientSettings.Value = SerializeToElement(jsonObject);

        await store.UpdateSettingsAsync(clientSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = clientSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/clients/{clientId}/secrets</c>, returning the secrets for the specified OpenID Client.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
    /// Handles <c>POST api/tenants/{tenantId}/clients/{clientId}/secrets</c>, generating a new server-side secret and persisting it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="request">The <see cref="CreateSecretRequest"/> describing the secret to generate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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

        var secretId = CryptoService.GenerateResourceId();

        PersistedSecret generatedSecret;
        try
        {
            generatedSecret = SecretGenerator.GenerateSecret(
                new GenerateSecretRequest
                {
                    SecretId = secretId,
                    SecretType = request.SecretType,
                    KeySizeBits = request.KeySizeBits,
                    Use = request.Use,
                    Algorithm = request.Algorithm,
                    CreatedWhen = TimeProvider.GetUtcNow(),
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
            await store.AddSecretAsync(clientId, generatedSecret, cancellationToken);
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

        // The store assigns the secret's token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = generatedSecret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}",
            ToSecretResource(generatedSecret)
        );
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}</c>, returning a single client secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
    /// Handles <c>PUT api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}</c>, updating a secret's metadata. Key material
    /// is immutable. Honors an <c>If-Match</c> precondition.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> with the new metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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

        var updated = new PersistedSecret
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
        };

        await store.UpdateSecretAsync(clientId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>DELETE api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}</c>, removing a client secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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

        await store.RemoveSecretAsync(clientId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/clients/{clientId}/owners</c>, returning the client's owners.
    /// </summary>
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
    /// Handles <c>POST api/tenants/{tenantId}/clients/{clientId}/owners</c>, granting a principal ownership of the client.
    /// </summary>
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
    /// Handles <c>DELETE api/tenants/{tenantId}/clients/{clientId}/owners/{principalId}</c>, revoking a principal's ownership of the
    /// client. Refused when it would leave the client with no owner.
    /// </summary>
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
}
