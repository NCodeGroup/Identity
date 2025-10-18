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

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

/*

GET api/server/settings
PUT api/server/settings

GET api/server/secrets
POST api/server/secrets
GET api/server/secrets/{secretId}
PUT api/server/secrets/{secretId}

GET api/tenants
GET api/tenants/{tenantId}
GET api/tenants/{tenantId}/settings
GET api/tenants/{tenantId}/secrets

*/

public class ServerApiEndpointHandler(
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

    internal virtual ServerResource ToServerResource(PersistedServer server)
    {
        return new ServerResource
        {
            ServerId = server.ServerId,
            ConcurrencyToken = server.ConcurrencyToken,
        };
    }

    internal virtual ServerSettingsResource ToServerSettingsResource(PersistedServerSettings settings)
    {
        return new ServerSettingsResource
        {
            ServerId = settings.ServerId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value
        };
    }

    internal virtual ServerSecretsResource ToServerSecretsResource(PersistedServerSecrets secrets)
    {
        return new ServerSecretsResource
        {
            ServerId = secrets.ServerId,
            ConcurrencyToken = secrets.ConcurrencyToken,
            Secrets = ToSecretsResource(secrets.Value)
        };
    }

    [EndpointName("api/server/get")]
    internal virtual async ValueTask<IResult> GetServerAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var server = await store.GetOrDefaultAsync(
            serverId,
            cancellationToken
        );

        return await ProcessGetAsync(
            httpContext,
            server,
            Operations.Read,
            ToServerResource
        );
    }

    [EndpointName("api/server/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(
            serverId,
            cancellationToken
        );

        return await ProcessGetAsync(
            httpContext,
            serverSettings,
            Operations.Read,
            ToServerSettingsResource
        );
    }

    [EndpointName("api/server/settings/update")]
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

        var serverSettings = await store.GetSettingsOrDefaultAsync(
            serverId,
            cancellationToken
        );

        if (serverSettings is null)
        {
            return TypedResults.NotFound();
        }

        var user = httpContext.User;
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            user,
            serverSettings,
            Operations.Update
        );

        if (authorizationResult.Succeeded)
        {
            // TODO: this can throw wrong element type
            var jsonObject = JsonObject.Create(serverSettings.Value) ?? new JsonObject();

            request.ApplyTo(jsonObject);

            serverSettings.Value = JsonSerializer.SerializeToElement(jsonObject);

            await store.UpdateSettingsAsync(serverSettings, cancellationToken);

            await storeManager.SaveChangesAsync(cancellationToken);

            return TypedResults.NoContent();
        }

        if (user.Identity?.IsAuthenticated ?? false)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Unauthorized();
    }

    [EndpointName("api/server/secrets/get")]
    internal virtual async ValueTask<IResult> GetSecretsAsync(
        HttpContext httpContext,
        [FromRoute] string serverId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverSecrets = await store.GetSecretsOrDefaultAsync(
            serverId,
            cancellationToken
        );

        return await ProcessGetAsync(
            httpContext,
            serverSecrets,
            Operations.Read,
            ToServerSecretsResource
        );
    }
}
