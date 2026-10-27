#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Contracts.Servers;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

/*

GET   api/servers/{serverId}

GET   api/servers/{serverId}/settings
PATCH api/servers/{serverId}/settings
GET   api/servers/{serverId}/effective-settings

GET    api/servers/{serverId}/secrets
POST   api/servers/{serverId}/secrets
GET    api/servers/{serverId}/secrets/{secretId}
GET    api/servers/{serverId}/secrets/{secretId}/public-key
PUT    api/servers/{serverId}/secrets/{secretId}
DELETE api/servers/{serverId}/secrets/{secretId}

*/

/// <summary>
/// Provides the API endpoints for managing an OpenID Server, including its settings and secrets.
/// </summary>
internal class ServerApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IServerValidator serverValidator,
    ISecretValidator secretValidator,
    ISecretGenerator secretGenerator,
    IClientKeyImporter keyImporter,
    ISecretPublicKeyConverter secretPublicKeyConverter,
    ISecretProtectorProvider secretProtectorProvider,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<ServerApiEndpointHandler> logger
) : BaseApiEndpointHandler, IEndpointProvider
{
    // The number of tenants read per page when a settings write is checked against every tenant's clients.
    private const int TenantPageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IServerValidator ServerValidator { get; } = serverValidator;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IClientKeyImporter KeyImporter { get; } = keyImporter;
    private ISecretPublicKeyConverter SecretPublicKeyConverter { get; } = secretPublicKeyConverter;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private ITenantClientCoherenceChecker TenantClientCoherenceChecker { get; } =
        tenantClientCoherenceChecker;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;
    private ILogger<ServerApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var servers = endpoints.MapGroup("/servers").WithTags(OpenIdConstants.EndpointTags.Servers);

        servers
            .MapPost("", CreateServerAsync)
            .Produces<ServerResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        servers.MapGet("", ListServersAsync).Produces<CollectionResource<ServerResource>>();
        servers.MapGet("/{serverId}", GetServerAsync).Produces<ServerResource>();
        servers.MapPatch("/{serverId}", UpdateServerAsync).Produces(StatusCodes.Status204NoContent);
        servers
            .MapDelete("/{serverId}", DeleteServerAsync)
            .Produces(StatusCodes.Status204NoContent);

        servers.MapGet("/{serverId}/settings", GetSettingsAsync).Produces<ServerSettingsResource>();
        servers
            .MapPatch("/{serverId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        servers
            .MapGet("/{serverId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<ServerEffectiveSettingsResource>();

        servers.MapGet("/{serverId}/secrets", GetSecretsAsync).Produces<ServerSecretsResource>();
        servers
            .MapPost("/{serverId}/secrets", CreateSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        servers
            .MapPost("/{serverId}/secrets/import", ImportSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        servers.MapGet("/{serverId}/secrets/{secretId}", GetSecretAsync).Produces<SecretResource>();
        servers
            .MapGet("/{serverId}/secrets/{secretId}/public-key", GetPublicKeyAsync)
            .Produces<PublicKeyResource>();
        servers
            .MapPut("/{serverId}/secrets/{secretId}", UpdateSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
        servers
            .MapDelete("/{serverId}/secrets/{secretId}", DeleteSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Maps a <see cref="PersistedServer"/> to its <see cref="ServerResource"/> representation.
    /// </summary>
    /// <param name="server">The <see cref="PersistedServer"/> to map.</param>
    /// <returns>The mapped <see cref="ServerResource"/>.</returns>
    internal virtual ServerResource ToServerResource(PersistedServer server)
    {
        return new ServerResource
        {
            ServerId = server.ServerId,
            ConcurrencyToken = server.ConcurrencyToken,
            DisplayName = server.DisplayName,
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedServerSettings"/> to its <see cref="ServerSettingsResource"/> representation.
    /// </summary>
    /// <param name="settings">The <see cref="PersistedServerSettings"/> to map.</param>
    /// <returns>The mapped <see cref="ServerSettingsResource"/>.</returns>
    internal virtual ServerSettingsResource ToServerSettingsResource(
        PersistedServerSettings settings
    )
    {
        return new ServerSettingsResource
        {
            ServerId = settings.ServerId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedServerSecrets"/> to its <see cref="ServerSecretsResource"/> representation.
    /// </summary>
    /// <param name="secrets">The <see cref="PersistedServerSecrets"/> to map.</param>
    /// <returns>The mapped <see cref="ServerSecretsResource"/>.</returns>
    internal virtual ServerSecretsResource ToServerSecretsResource(PersistedServerSecrets secrets)
    {
        return new ServerSecretsResource
        {
            ServerId = secrets.ServerId,
            ConcurrencyToken = secrets.ConcurrencyToken,
            Secrets = ToSecretsResource(secrets.Value),
        };
    }

    /// <summary>
    /// Lists the OpenID servers.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of servers to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of servers.</response>
    [EndpointName("api/servers/list")]
    internal virtual async ValueTask<IResult> ListServersAsync(
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        // Server administration is central-admin; a null authorization resource restricts this to global admins.
        return await ProcessListAsync(
            httpContext,
            authorizationResource: null,
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IServerStore>();
                return await store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToServerResource
        );
    }

    /// <summary>
    /// Gets an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested server.</response>
    [EndpointName("api/servers/get")]
    internal virtual async ValueTask<IResult> GetServerAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var server = await store.GetOrDefaultAsync(serverId, cancellationToken);

        return await ProcessGetAsync(httpContext, server, Operations.Read, ToServerResource);
    }

    /// <summary>
    /// Creates an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="request">The <see cref="CreateServerRequest"/> describing the server to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created server.</response>
    [EndpointName("api/servers/create")]
    internal virtual async ValueTask<IResult> CreateServerAsync(
        HttpContext httpContext,
        [FromBody] CreateServerRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverId = CryptoService.GenerateResourceId();

        var server = new PersistedServer
        {
            ServerId = serverId,
            ConcurrencyToken = string.Empty,
            DisplayName = request.DisplayName,
            Settings = new PersistedServerSettings
            {
                ServerId = serverId,
                ConcurrencyToken = string.Empty,
                Value = SerializeToElement(ToJsonObject(request.Settings)),
            },
            Secrets = new PersistedServerSecrets
            {
                ServerId = serverId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        // Core preconditions (authorization + uniqueness) are asserted by the validator (ADR-0014).
        var error = await ServerValidator.ValidateCreateAsync(
            httpContext.User,
            server,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // Setting names are a closed vocabulary (ADR-0139), and the server carries only the settings that apply within it
        // (ADR-0149): a settings document that cannot be honored is rejected here rather than stored and failed when the
        // server's settings are next loaded.
        var jsonOptions = httpContext.GetOpenIdContext().Environment.JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.Server,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        await store.AddAsync(server, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        var resource = ToServerResource(server);

        await ManagementAuditRecorder.RecordServerChangedAsync(
            httpContext,
            server.ServerId,
            ResourceChangeTypes.Created,
            ToResourceValues(resource),
            cancellationToken
        );

        // The store assigns the row token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = server.ConcurrencyToken;
        return TypedResults.Created($"/servers/{server.ServerId}", resource);
    }

    /// <summary>
    /// Updates an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="request">The JSON Patch document to apply to the server metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The server was updated.</response>
    [EndpointName("api/servers/update")]
    internal virtual async ValueTask<IResult> UpdateServerAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromBody] JsonPatchDocument<UpdateServerRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var server = await store.GetOrDefaultAsync(serverId, cancellationToken);
        if (server is null)
        {
            return TypedResults.NotFound();
        }

        var model = new UpdateServerRequest { DisplayName = server.DisplayName };

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

        // Core preconditions (authorization + If-Match + required fields) are asserted by the validator
        // against the hydrated model (ADR-0014).
        var error = await ServerValidator.ValidateUpdateAsync(
            httpContext.User,
            server,
            model,
            ifMatch,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        server.DisplayName = model.DisplayName;

        await store.UpdateAsync(server, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordServerChangedAsync(
            httpContext,
            server.ServerId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToServerResource(server)),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The server was deleted.</response>
    /// <response code="409">The server still has secrets and cannot be deleted.</response>
    [EndpointName("api/servers/delete")]
    internal virtual async ValueTask<IResult> DeleteServerAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var server = await store.GetOrDefaultAsync(serverId, cancellationToken);
        if (server is null)
        {
            return TypedResults.NotFound();
        }

        // Core preconditions (authorization + no-dependents) are asserted by the validator (ADR-0014).
        var error = await ServerValidator.ValidateDeleteAsync(
            httpContext.User,
            server,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var resourceValues = ToResourceValues(ToServerResource(server));

        await store.RemoveAsync(serverId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordServerChangedAsync(
            httpContext,
            server.ServerId,
            ResourceChangeTypes.Deleted,
            resourceValues,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a server's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The server's settings.</response>
    [EndpointName("api/servers/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(serverId, cancellationToken);

        return await ProcessGetAsync(
            httpContext,
            serverSettings,
            Operations.Read,
            ToServerSettingsResource
        );
    }

    /// <summary>
    /// Gets a server's effective (resolved) settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The server's effective settings.</response>
    [EndpointName("api/servers/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(serverId, cancellationToken);
        if (serverSettings is null)
        {
            return TypedResults.NotFound();
        }

        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // The authorization is against the persisted settings, like the raw settings read. See ADR-0245.
        return await ProcessGetAsync(
            httpContext,
            serverSettings.Value,
            authorizationResource: serverSettings,
            Operations.Read,
            async (settings, token) =>
            {
                var effectiveSettings = await ResolveStoredServerSettingsAsync(
                    serverId,
                    settings,
                    token
                );
                return ToServerEffectiveSettingsResource(serverId, effectiveSettings);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Resolves the effective settings of the server's committed settings document and names the server when the
    /// document cannot be honored by the settings vocabulary.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenID Server that owns the settings document.</param>
    /// <param name="settings">The server's committed settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The server's effective settings.</returns>
    /// <exception cref="UnreadableSettingsException">The document cannot be honored.</exception>
    // See ADR-0245.
    private async ValueTask<IReadOnlySettingCollection> ResolveStoredServerSettingsAsync(
        string serverId,
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await EffectiveSettingsResolver.ResolveServerAsync(settings, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new UnreadableSettingsException(OpenIdSettingScopes.Server, serverId, exception);
        }
    }

    /// <summary>
    /// Maps an OpenID Server's effective <see cref="IReadOnlySettingCollection"/> to its
    /// <see cref="ServerEffectiveSettingsResource"/> representation.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenID Server that owns the settings.</param>
    /// <param name="settings">The server's effective <see cref="IReadOnlySettingCollection"/>.</param>
    /// <returns>The mapped <see cref="ServerEffectiveSettingsResource"/>.</returns>
    internal virtual ServerEffectiveSettingsResource ToServerEffectiveSettingsResource(
        string serverId,
        IReadOnlySettingCollection settings
    )
    {
        return new ServerEffectiveSettingsResource
        {
            ServerId = serverId,
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.Server
            ),
        };
    }

    /// <summary>
    /// Updates a server's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The server's settings were updated.</response>
    [EndpointName("api/servers/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(serverId, cancellationToken);

        if (serverSettings is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            serverSettings,
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        // Enforce the optimistic-concurrency precondition supplied by the caller. Without this check the
        // row we just loaded would always pass the store's concurrency comparison, silently overwriting
        // any changes made between the caller's read and this write (lost update).
        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, serverSettings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(serverSettings.Value);

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
            serverSettings.Value,
            settingsValue,
            OpenIdSettingScopes.Server,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var currentServerSettings = await ResolveReplacedServerSettingsOrDefaultAsync(
            serverId,
            serverSettings.Value,
            cancellationToken
        );
        var newServerSettings = await EffectiveSettingsResolver.ResolveServerAsync(
            settingsValue,
            cancellationToken
        );

        if (
            currentServerSettings is not null
            && ClientCoherenceValidator.ReadsChangedSetting(
                currentServerSettings,
                newServerSettings
            )
        )
        {
            var coherenceErrors = await ValidateClientCoherenceAsync(
                currentServerSettings,
                newServerSettings,
                storeManager,
                cancellationToken
            );
            if (coherenceErrors.Count > 0)
            {
                return SettingsValidationProblem(coherenceErrors);
            }
        }

        var previousSettings = serverSettings.Value;
        serverSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(serverSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        await NotifyServerSettingsChangedAsync(
            httpContext,
            serverId,
            previousSettings,
            settingsValue,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = serverSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Resolves the effective settings of the server's committed settings document, which a settings write replaces.
    /// When the document cannot be honored by the settings vocabulary, nothing was coherent under it, so no client is
    /// checked and the write may replace it with a document that can be honored.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="replacedSettings">The server's committed settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The server's effective settings, or <see langword="null"/> when the document cannot be honored.</returns>
    // See ADR-0241.
    private async ValueTask<IReadOnlySettingCollection?> ResolveReplacedServerSettingsOrDefaultAsync(
        string serverId,
        JsonElement replacedSettings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await EffectiveSettingsResolver.ResolveServerAsync(
                replacedSettings,
                cancellationToken
            );
        }
        catch (JsonException exception)
        {
            Logger.ServerSettingsUnreadable(serverId, exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Verifies that replacing the server's settings document leaves every value each client of every tenant registers
    /// permitted by every client coherence rule (<see cref="ITenantClientCoherenceChecker"/>). The current and new
    /// server settings are each resolved through the <see cref="IEffectiveSettingsResolver"/>, and each tenant's
    /// document is resolved onto both. The tenants are read as an untracked, secret-free projection
    /// (<see cref="ITenantStore.GetSettingsPageAsync"/>), and the walk runs only when the effective value of a setting
    /// a coherence rule reads changed (<see cref="IClientCoherenceValidator.ReadsChangedSetting"/>). A tenant whose
    /// settings document cannot be honored is logged and skipped, because none of its clients is coherent under either
    /// settings.
    /// </summary>
    /// <param name="currentServerSettings">The server's effective settings with its current settings document.</param>
    /// <param name="newServerSettings">The server's effective settings with its new settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenants and their clients.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances, keyed by the server setting that does not permit each value, or
    /// an empty list.</returns>
    // See ADR-0206 and ADR-0241.
    internal virtual async ValueTask<IReadOnlyList<SettingError>> ValidateClientCoherenceAsync(
        IReadOnlySettingCollection currentServerSettings,
        IReadOnlySettingCollection newServerSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        List<SettingError> errors = [];
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var page = new PageRequest { Limit = TenantPageSize };
        do
        {
            var result = await tenantStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var tenantSettings in result.Items)
            {
                IReadOnlySettingCollection currentTenantSettings;
                IReadOnlySettingCollection newTenantSettings;
                try
                {
                    currentTenantSettings = EffectiveSettingsResolver.Resolve(
                        currentServerSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                    newTenantSettings = EffectiveSettingsResolver.Resolve(
                        newServerSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                }
                catch (JsonException exception)
                {
                    Logger.TenantSettingsUnreadable(tenantSettings.TenantId, exception.Message);
                    continue;
                }

                var findings = await TenantClientCoherenceChecker.FindIntroducedViolationsAsync(
                    storeManager,
                    tenantSettings.TenantId,
                    currentTenantSettings,
                    newTenantSettings,
                    cancellationToken
                );

                errors.AddRange(
                    findings.Select(finding => new SettingError(
                        finding.Violation.GoverningSettingName,
                        $"The change would make the client '{finding.ClientId}' of tenant '{tenantSettings.TenantId}' incoherent: {finding.Violation.Description}"
                    ))
                );
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return errors;
    }

    /// <summary>
    /// Lists a server's secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The server's secrets.</response>
    [EndpointName("api/servers/secrets/get")]
    internal virtual async ValueTask<IResult> GetSecretsAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverSecrets = await store.GetSecretsOrDefaultAsync(serverId, cancellationToken);

        return await ProcessGetAsync(
            httpContext,
            serverSecrets,
            Operations.Read,
            ToServerSecretsResource
        );
    }

    /// <summary>
    /// Creates a server secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="request">The <see cref="CreateSecretRequest"/> describing the secret to generate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created secret.</response>
    [EndpointName("api/servers/secrets/create")]
    internal virtual async ValueTask<IResult> CreateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromBody] CreateSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var server = await store.GetOrDefaultAsync(serverId, cancellationToken);
        if (server is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            server,
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

        var secretId = CryptoService.GenerateResourceId();

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForServer(serverId),
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
                    KeySizeBits = request.KeySizeBits,
                    Curve = request.Curve,
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
            await store.AddSecretAsync(serverId, generatedSecret.Secret, cancellationToken);
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

        await ManagementAuditRecorder.RecordServerSecretChangedAsync(
            httpContext,
            serverId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(secretResource),
            cancellationToken
        );

        // The store assigns the secret's token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;
        return TypedResults.Created($"/servers/{serverId}/secrets/{secretId}", secretResource);
    }

    /// <summary>
    /// Imports a caller-supplied public key (JWK, PEM, or certificate) as a new server secret.
    /// </summary>
    // See ADR-0104.
    internal virtual async ValueTask<IResult> ImportSecretAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromBody] ImportSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var server = await store.GetOrDefaultAsync(serverId, cancellationToken);
        if (server is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            server,
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
        if (!KeyImporter.TryImportKey(importRequest, out var importedSecret, out var importError))
        {
            return TypedResults.Problem(
                detail: importError,
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var keyIdConflict = KeyIdConflictOrDefault(server.Secrets.Value, importedSecret, "server");
        if (keyIdConflict is not null)
        {
            return keyIdConflict;
        }

        try
        {
            await store.AddSecretAsync(serverId, importedSecret, cancellationToken);
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

        await ManagementAuditRecorder.RecordServerSecretChangedAsync(
            httpContext,
            serverId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(importedSecretResource),
            cancellationToken
        );

        httpContext.Response.Headers.ETag = importedSecret.ConcurrencyToken;
        return TypedResults.Created(
            $"/servers/{serverId}/secrets/{secretId}",
            importedSecretResource
        );
    }

    /// <summary>
    /// Gets a server secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested secret.</response>
    [EndpointName("api/servers/secrets/get-one")]
    internal virtual async ValueTask<IResult> GetSecretAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var secret = await store.GetSecretOrDefaultAsync(serverId, secretId, cancellationToken);

        return await ProcessGetAsync(httpContext, secret, Operations.Read, ToSecretResource);
    }

    /// <summary>
    /// Gets the public key material of an asymmetric server secret, as a JWK and a PEM.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The public key material of the requested secret.</response>
    [EndpointName("api/servers/secrets/get-public-key")]
    internal virtual async ValueTask<IResult> GetPublicKeyAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var secret = await store.GetSecretOrDefaultAsync(serverId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        return await ProcessGetAsync(
            httpContext,
            secret,
            authorizationResource: null,
            Operations.Read,
            // The public key is read from its stored public half, so no master key is loaded (ADR-0268).
            x => SecretPublicKeyConverter.Convert(x)
        );
    }

    /// <summary>
    /// Updates a server secret's metadata.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> with the new metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was updated.</response>
    [EndpointName("api/servers/secrets/update")]
    internal virtual async ValueTask<IResult> UpdateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromRoute] string secretId,
        [FromBody] UpdateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var secret = await store.GetSecretOrDefaultAsync(serverId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            secret,
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

        await store.UpdateSecretAsync(serverId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordServerSecretChangedAsync(
            httpContext,
            serverId,
            secretId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToSecretResource(updated)),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a server secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was deleted.</response>
    [EndpointName("api/servers/secrets/delete")]
    internal virtual async ValueTask<IResult> DeleteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var secret = await store.GetSecretOrDefaultAsync(serverId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            secret,
            Operations.Delete
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var secretValues = ToResourceValues(ToSecretResource(secret));

        await store.RemoveSecretAsync(serverId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordServerSecretChangedAsync(
            httpContext,
            serverId,
            secretId,
            ResourceChangeTypes.Deleted,
            secretValues,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    // Announces a committed change to the server's settings document, so every handler of the change (the default one
    // reads the document again on every replica, whose reconciliation then seeds the tenants and checks the clients,
    // ADR-0219) runs after the write. The mediator is request-scoped, so it is resolved from the request's services.
    private static ValueTask NotifyServerSettingsChangedAsync(
        HttpContext httpContext,
        string serverId,
        JsonElement previousSettings,
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return mediator.SendAsync(
            new ServerSettingsChangedCommand(serverId, previousSettings.Clone(), settings.Clone()),
            cancellationToken
        );
    }
}
