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
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

/*

GET   api/servers/{serverId}

GET   api/servers/{serverId}/settings
PATCH api/servers/{serverId}/settings

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
    IAuthorizationService authorizationService
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var servers = endpoints.MapGroup("/servers");

        servers.MapGet("/{serverId}", GetServerAsync);

        servers.MapGet("/{serverId}/settings", GetSettingsAsync);
        servers.MapPatch("/{serverId}/settings", UpdateSettingsAsync);

        servers.MapGet("/{serverId}/secrets", GetSecretsAsync);
    }

    /// <summary>
    /// Serializes the specified <see cref="JsonObject"/> into a detached <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to serialize.</param>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    internal virtual JsonElement SerializeToElement(JsonObject jsonObject)
    {
        return JsonSerializer.SerializeToElement(jsonObject);
    }

    /// <summary>
    /// Converts the specified <see cref="JsonElement"/> into a mutable <see cref="JsonObject"/>, returning an empty
    /// object when the element is <c>null</c> or not a JSON object.
    /// </summary>
    /// <param name="element">The <see cref="JsonElement"/> to convert.</param>
    /// <returns>The resulting <see cref="JsonObject"/>.</returns>
    internal virtual JsonObject ToJsonObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Object:
                return JsonObject.Create(element) ?? new JsonObject();

            default:
                return new JsonObject();
        }
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
    /// Handles <c>GET api/servers/{serverId}</c>, returning the specified OpenID Server resource.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
    /// Handles <c>GET api/servers/{serverId}/settings</c>, returning the settings for the specified OpenID Server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
    /// Handles <c>PATCH api/servers/{serverId}/settings</c>, applying a JSON Patch document to the settings for the
    /// specified OpenID Server. Honors an <c>If-Match</c> precondition and returns the refreshed <c>ETag</c>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
            return TypedResults.Problem(
                detail: exception.Message,
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
    /// Handles <c>GET api/servers/{serverId}/secrets</c>, returning the secrets for the specified OpenID Server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="serverId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
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
}
