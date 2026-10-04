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
using Microsoft.Extensions.Logging;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Contracts.Servers;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
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
    ISecretGenerator secretGenerator,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IOpenIdServerProvider serverProvider,
    ILogger<ServerApiEndpointHandler> logger
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IServerValidator ServerValidator { get; } = serverValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IOpenIdServerProvider ServerProvider { get; } = serverProvider;
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
            .Produces<ServerResource>(StatusCodes.Status201Created);
        servers.MapGet("", ListServersAsync).Produces<CollectionResource<ServerResource>>();
        servers.MapGet("/{serverId}", GetServerAsync).Produces<ServerResource>();
        servers
            .MapDelete("/{serverId}", DeleteServerAsync)
            .Produces(StatusCodes.Status204NoContent);

        servers.MapGet("/{serverId}/settings", GetSettingsAsync).Produces<ServerSettingsResource>();
        servers
            .MapPatch("/{serverId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent);
        servers
            .MapGet("/{serverId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<ServerEffectiveSettingsResource>();

        servers.MapGet("/{serverId}/secrets", GetSecretsAsync).Produces<ServerSecretsResource>();
        servers
            .MapPost("/{serverId}/secrets", CreateSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        servers.MapGet("/{serverId}/secrets/{secretId}", GetSecretAsync).Produces<SecretResource>();
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
                return await store.GetPageAsync(cursor, effectiveLimit, cancellationToken);
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

        await store.AddAsync(server, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        // The store assigns the row token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = server.ConcurrencyToken;
        return TypedResults.Created($"/servers/{server.ServerId}", ToServerResource(server));
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

        await store.RemoveAsync(serverId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

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

        var openIdContext = httpContext.GetOpenIdContext();
        var server = await ServerProvider.GetAsync(openIdContext.Environment, cancellationToken);

        // Authorize like the raw settings read (against the persisted settings); the body is the resolved view.
        return await ProcessGetAsync(
            httpContext,
            server.SettingsProvider.Collection,
            authorizationResource: serverSettings,
            Operations.Read,
            settings => ToServerEffectiveSettingsResource(serverId, settings)
        );
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
            Settings = ToEffectiveSettingsElement(settings),
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

        serverSettings.Value = SerializeToElement(jsonObject);

        await store.UpdateSettingsAsync(serverSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = serverSettings.ConcurrencyToken;

        return TypedResults.NoContent();
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
            await store.AddSecretAsync(serverId, generatedSecret, cancellationToken);
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
            $"/servers/{serverId}/secrets/{secretId}",
            ToSecretResource(generatedSecret)
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

        await store.UpdateSecretAsync(serverId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

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

        await store.RemoveSecretAsync(serverId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
