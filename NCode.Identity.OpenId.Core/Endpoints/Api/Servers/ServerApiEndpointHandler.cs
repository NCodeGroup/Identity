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
using NCode.Identity.OpenId.Endpoints.Api.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using SystemTextJsonPatch;

namespace NCode.Identity.OpenId.Endpoints.Api.Servers;

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
) : BaseApiEndpointHandler, IOpenIdEndpointProvider
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
    }

    internal virtual ServerResource ToServerResource(PersistedServer server)
    {
        return new ServerResource
        {
            ServerId = server.ServerId,
            ConcurrencyToken = server.ConcurrencyToken,
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

        var serverOrNull = await store.TryGetByServerIdAsync(
            serverId,
            cancellationToken
        );

        var resourceOrNull = serverOrNull is not null ? ToServerResource(serverOrNull) : null;

        return await ProcessGetAsync(
            httpContext,
            resourceOrNull,
            ResourceOperations.Servers.Basic.Read
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

        var settingsStateOrNull = await store.GetSettingsOrDefaultAsync(
            serverId,
            lastKnownState: null,
            cancellationToken
        );

        var resourceOrNull = settingsStateOrNull.HasValue ?
            new ServerSettingsResource
            {
                ServerId = serverId,
                ConcurrencyToken = settingsStateOrNull.Value.ConcurrencyToken,
                Settings = settingsStateOrNull.Value.Value
            } :
            null;

        return await ProcessGetAsync(
            httpContext,
            resourceOrNull,
            ResourceOperations.Servers.Settings.Read,
            resource => resource.Settings
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

        var settingsStateOrNull = await store.GetSettingsOrDefaultAsync(
            serverId,
            lastKnownState: null,
            cancellationToken
        );

        if (settingsStateOrNull is null)
        {
            return TypedResults.NotFound();
        }

        var settingsState = settingsStateOrNull.Value;
        var settingsJson = settingsState.Value;

        var user = httpContext.User;
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            user,
            ServerResourceWrapper.Create(serverId, settingsState),
            ResourceOperations.Servers.Settings.Update
        );

        if (authorizationResult.Succeeded)
        {
            // TODO: this can throw wrong element type
            var jsonObject = JsonObject.Create(settingsJson) ?? new JsonObject();

            request.ApplyTo(jsonObject);

            // TODO: json serializer options
            var newSettingsJson = JsonSerializer.SerializeToElement(jsonObject);

            throw new NotImplementedException();
        }

        if (user.Identity?.IsAuthenticated ?? false)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Unauthorized();
    }
}
