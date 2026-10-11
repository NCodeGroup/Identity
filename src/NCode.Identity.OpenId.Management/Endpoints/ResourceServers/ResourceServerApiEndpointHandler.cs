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
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
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

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/*

GET    api/tenants/{tenantId}/resource-servers
POST   api/tenants/{tenantId}/resource-servers
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}
PATCH  api/tenants/{tenantId}/resource-servers/{resourceServerId}
DELETE api/tenants/{tenantId}/resource-servers/{resourceServerId}

GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes
POST   api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{scopeValue}
PUT    api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{scopeValue}
DELETE api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{scopeValue}

GET   api/tenants/{tenantId}/resource-servers/{resourceServerId}/settings
PATCH api/tenants/{tenantId}/resource-servers/{resourceServerId}/settings
GET   api/tenants/{tenantId}/resource-servers/{resourceServerId}/effective-settings

GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets
POST   api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}/public-key
PUT    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}
DELETE api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}

*/

/// <summary>
/// Provides the API endpoints for managing resource servers (APIs, in Auth0 terms), including their scopes, settings,
/// and secrets.
/// </summary>
internal class ResourceServerApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IResourceServerValidator resourceServerValidator,
    ISecretValidator secretValidator,
    ISecretGenerator secretGenerator,
    ISecretPublicKeyConverter secretPublicKeyConverter,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    IOpenIdServerProvider serverProvider,
    IResourceOwnershipService resourceOwnershipService,
    ILogger<ResourceServerApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IResourceServerValidator ResourceServerValidator { get; } = resourceServerValidator;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ISecretPublicKeyConverter SecretPublicKeyConverter { get; } = secretPublicKeyConverter;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private IOpenIdServerProvider ServerProvider { get; } = serverProvider;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<ResourceServerApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var resourceServers = endpoints
            .MapGroup("/resource-servers")
            .WithTags(OpenIdConstants.EndpointTags.ResourceServers);

        resourceServers
            .MapGet("", ListAsync)
            .Produces<CollectionResource<ResourceServerResource>>();
        resourceServers
            .MapPost("", CreateAsync)
            .Produces<ResourceServerResource>(StatusCodes.Status201Created);
        resourceServers.MapGet("/{resourceServerId}", GetAsync).Produces<ResourceServerResource>();
        resourceServers
            .MapPatch("/{resourceServerId}", UpdateAsync)
            .Produces(StatusCodes.Status204NoContent);
        resourceServers
            .MapDelete("/{resourceServerId}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent);

        resourceServers
            .MapGet("/{resourceServerId}/scopes", ListScopesAsync)
            .Produces<IReadOnlyList<ScopeResource>>();
        resourceServers
            .MapPost("/{resourceServerId}/scopes", CreateScopeAsync)
            .Produces<ScopeResource>(StatusCodes.Status201Created);
        resourceServers
            .MapGet("/{resourceServerId}/scopes/{scopeValue}", GetScopeAsync)
            .Produces<ScopeResource>();
        resourceServers
            .MapPut("/{resourceServerId}/scopes/{scopeValue}", UpdateScopeAsync)
            .Produces(StatusCodes.Status204NoContent);
        resourceServers
            .MapDelete("/{resourceServerId}/scopes/{scopeValue}", DeleteScopeAsync)
            .Produces(StatusCodes.Status204NoContent);

        resourceServers
            .MapGet("/{resourceServerId}/settings", GetSettingsAsync)
            .Produces<ResourceServerSettingsResource>();
        resourceServers
            .MapPatch("/{resourceServerId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent);
        resourceServers
            .MapGet("/{resourceServerId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<ResourceServerEffectiveSettingsResource>();

        resourceServers
            .MapGet("/{resourceServerId}/secrets", GetSecretsAsync)
            .Produces<ResourceServerSecretsResource>();
        resourceServers
            .MapPost("/{resourceServerId}/secrets", CreateSecretAsync)
            .Produces<CreatedSecretResource>(StatusCodes.Status201Created);
        resourceServers
            .MapGet("/{resourceServerId}/secrets/{secretId}", GetSecretAsync)
            .Produces<SecretResource>();
        resourceServers
            .MapGet("/{resourceServerId}/secrets/{secretId}/public-key", GetPublicKeyAsync)
            .Produces<PublicKeyResource>();
        resourceServers
            .MapPut("/{resourceServerId}/secrets/{secretId}", UpdateSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
        resourceServers
            .MapDelete("/{resourceServerId}/secrets/{secretId}", DeleteSecretAsync)
            .Produces(StatusCodes.Status204NoContent);

        resourceServers
            .MapGet("/{resourceServerId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        resourceServers
            .MapPost("/{resourceServerId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        resourceServers
            .MapDelete("/{resourceServerId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    internal virtual ScopeResource ToScopeResource(PersistedScope scope) =>
        new()
        {
            Value = scope.Value,
            Description = scope.Description,
            IsSystem = scope.IsSystem,
        };

    internal virtual ResourceServerResource ToResourceServerResource(
        PersistedResourceServer resourceServer
    ) =>
        new()
        {
            TenantId = resourceServer.TenantId,
            ResourceServerId = resourceServer.ResourceServerId,
            Identifier = resourceServer.Identifier,
            ConcurrencyToken = resourceServer.ConcurrencyToken,
            Name = resourceServer.Name,
            IsSystem = resourceServer.IsSystem,
            IsDisabled = resourceServer.IsDisabled,
            Scopes = resourceServer.Scopes.Select(ToScopeResource).ToList(),
        };

    /// <summary>
    /// Lists the resource servers (APIs) in a tenant.
    /// </summary>
    /// <response code="200">A page of resource servers.</response>
    [EndpointName("api/resource-servers/list")]
    internal virtual async ValueTask<IResult> ListAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IResourceServerStore>();
                return await store.GetPageAsync(cursor, effectiveLimit, cancellationToken);
            },
            ToResourceServerResource
        );
    }

    /// <summary>
    /// Gets a resource server (API).
    /// </summary>
    /// <response code="200">The requested resource server.</response>
    [EndpointName("api/resource-servers/get")]
    internal virtual async ValueTask<IResult> GetAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            resourceServer,
            node,
            Operations.Read,
            ToResourceServerResource
        );
    }

    /// <summary>
    /// Creates a resource server (API).
    /// </summary>
    /// <response code="201">The created resource server.</response>
    [EndpointName("api/resource-servers/create")]
    internal virtual async ValueTask<IResult> CreateAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateResourceServerRequest request,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerId = CryptoService.GenerateResourceId();

        var resourceServer = new PersistedResourceServer
        {
            TenantId = tenantId,
            ResourceServerId = resourceServerId,
            Identifier = request.Identifier,
            ConcurrencyToken = string.Empty,
            Name = request.Name,
            IsSystem = false,
            IsDisabled = request.IsDisabled,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = tenantId,
                ResourceServerId = resourceServerId,
                Value = request.Settings,
            },
            Scopes = [],
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = tenantId,
                ResourceServerId = resourceServerId,
                Value = [],
            },
        };

        // Core preconditions (authorization + identifier uniqueness) are asserted by the validator (ADR-0014).
        var error = await ResourceServerValidator.ValidateCreateAsync(
            httpContext.User,
            resourceServer,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        await store.AddAsync(resourceServer, cancellationToken);
        await ResourceOwnershipService.AssignCreatorAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            resourceServer.TenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServer.ResourceServerId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = resourceServer.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}",
            ToResourceServerResource(resourceServer)
        );
    }

    /// <summary>
    /// Updates a resource server (API).
    /// </summary>
    /// <response code="204">The resource server was updated.</response>
    [EndpointName("api/resource-servers/update")]
    internal virtual async ValueTask<IResult> UpdateAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromBody] JsonPatchDocument<UpdateResourceServerRequest> request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var error = await ResourceServerValidator.ValidateUpdateAsync(
            httpContext.User,
            resourceServer,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var model = new UpdateResourceServerRequest
        {
            Name = resourceServer.Name,
            IsDisabled = resourceServer.IsDisabled,
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

        resourceServer.Name = model.Name;
        resourceServer.IsDisabled = model.IsDisabled;

        await store.UpdateAsync(resourceServer, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a resource server (API).
    /// </summary>
    /// <response code="204">The resource server was deleted.</response>
    /// <response code="409">The resource server is a system server or is still referenced by a client grant.</response>
    [EndpointName("api/resource-servers/delete")]
    internal virtual async ValueTask<IResult> DeleteAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var error = await ResourceServerValidator.ValidateDeleteAsync(
            httpContext.User,
            resourceServer,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        await store.RemoveAsync(resourceServerId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a resource server's scopes.
    /// </summary>
    /// <response code="200">The resource server's scopes.</response>
    [EndpointName("api/resource-servers/scopes/list")]
    internal virtual async ValueTask<IResult> ListScopesAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);

        return await ProcessGetAsync(
            httpContext,
            resourceServer,
            Operations.Read,
            value => (IReadOnlyList<ScopeResource>)value.Scopes.Select(ToScopeResource).ToList()
        );
    }

    /// <summary>
    /// Creates a scope on a resource server.
    /// </summary>
    /// <response code="201">The created scope.</response>
    /// <response code="409">A scope with the same value already exists.</response>
    [EndpointName("api/resource-servers/scopes/create")]
    internal virtual async ValueTask<IResult> CreateScopeAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] CreateScopeRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            ToResourceServerResource(resourceServer),
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var persistedScope = new PersistedScope
        {
            Value = request.Value,
            Description = request.Description,
            IsSystem = false,
        };

        try
        {
            await store.AddScopeAsync(resourceServerId, persistedScope, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: "A scope with the specified value already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        return TypedResults.Created(
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{request.Value}",
            ToScopeResource(persistedScope)
        );
    }

    /// <summary>
    /// Gets a scope on a resource server.
    /// </summary>
    /// <response code="200">The requested scope.</response>
    [EndpointName("api/resource-servers/scopes/get")]
    internal virtual async ValueTask<IResult> GetScopeAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromRoute] string scopeValue,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        var scope = resourceServer?.Scopes.FirstOrDefault(scope =>
            string.Equals(scope.Value, scopeValue, StringComparison.OrdinalIgnoreCase)
        );

        return await ProcessGetAsync(httpContext, scope, Operations.Read, ToScopeResource);
    }

    /// <summary>
    /// Updates a scope on a resource server.
    /// </summary>
    /// <response code="204">The scope was updated.</response>
    [EndpointName("api/resource-servers/scopes/update")]
    internal virtual async ValueTask<IResult> UpdateScopeAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromRoute] string scopeValue,
        [FromBody] UpdateScopeRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            ToResourceServerResource(resourceServer),
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        try
        {
            await store.UpdateScopeAsync(
                resourceServerId,
                new PersistedScope
                {
                    Value = scopeValue,
                    Description = request.Description,
                    IsSystem = false,
                },
                cancellationToken
            );
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a scope on a resource server.
    /// </summary>
    /// <response code="204">The scope was deleted.</response>
    /// <response code="409">The scope is a system scope and cannot be removed.</response>
    [EndpointName("api/resource-servers/scopes/delete")]
    internal virtual async ValueTask<IResult> DeleteScopeAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromRoute] string scopeValue,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            ToResourceServerResource(resourceServer),
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        bool removed;
        try
        {
            removed = await store.RemoveScopeAsync(resourceServerId, scopeValue, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict
            );
        }

        if (!removed)
        {
            return TypedResults.NotFound();
        }

        await storeManager.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a resource server's owners.
    /// </summary>
    /// <response code="200">A page of the resource server's owners.</response>
    [EndpointName("api/resource-servers/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessListOwnersAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId,
            (storeManager, token) =>
                ResourceServerExistsAsync(storeManager, resourceServerId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Adds an owner to a resource server.
    /// </summary>
    /// <response code="201">The owner was added.</response>
    [EndpointName("api/resource-servers/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    )
    {
        return await ProcessAddOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId,
            request,
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}/owners",
            (storeManager, token) =>
                ResourceServerExistsAsync(storeManager, resourceServerId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Removes an owner from a resource server.
    /// </summary>
    /// <response code="204">The owner was removed.</response>
    [EndpointName("api/resource-servers/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessRemoveOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId,
            principalId,
            cancellationToken
        );
    }

    private static async ValueTask<bool> ResourceServerExistsAsync(
        IStoreManager storeManager,
        string resourceServerId,
        CancellationToken cancellationToken
    ) =>
        await storeManager
            .GetStore<IResourceServerStore>()
            .GetOrDefaultAsync(resourceServerId, cancellationToken)
            is not null;

    // Settings and secrets

    internal virtual ResourceServerSettingsResource ToResourceServerSettingsResource(
        PersistedResourceServerSettings settings
    ) =>
        new()
        {
            TenantId = settings.TenantId,
            ResourceServerId = settings.ResourceServerId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };

    internal virtual ResourceServerSecretsResource ToResourceServerSecretsResource(
        PersistedResourceServerSecrets secrets
    ) =>
        new()
        {
            TenantId = secrets.TenantId,
            ResourceServerId = secrets.ResourceServerId,
            ConcurrencyToken = secrets.ConcurrencyToken,
            Secrets = ToSecretsResource(secrets.Value),
        };

    internal virtual ResourceServerEffectiveSettingsResource ToResourceServerEffectiveSettingsResource(
        PersistedResourceServer resourceServer,
        IReadOnlySettingCollection settings
    ) =>
        new()
        {
            TenantId = resourceServer.TenantId,
            ResourceServerId = resourceServer.ResourceServerId,
            Settings = ToEffectiveSettingsElement(settings),
        };

    /// <summary>
    /// Merges a resource server's persisted settings onto the addressed tenant's effective settings, producing the same
    /// effective <see cref="IReadOnlySettingCollection"/> the authorization server resolves at runtime (server baseline
    /// merged with the tenant's and then the resource server's persisted overrides).
    /// </summary>
    internal virtual async ValueTask<IReadOnlySettingCollection?> MergeResourceServerSettingsAsync(
        OpenIdContext openIdContext,
        string tenantId,
        PersistedResourceServer resourceServer,
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
            SettingSerializer.DeserializeSettings(resourceServer.Settings.Value, jsonOptions)
        );
    }

    /// <summary>
    /// Gets a resource server's persisted settings.
    /// </summary>
    /// <response code="200">The resource server's settings.</response>
    [EndpointName("api/resource-servers/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            resourceServer?.Settings,
            node,
            Operations.Read,
            ToResourceServerSettingsResource
        );
    }

    /// <summary>
    /// Updates a resource server's persisted settings via a JSON Patch document.
    /// </summary>
    /// <response code="204">The settings were updated.</response>
    /// <response code="412">The supplied <c>If-Match</c> concurrency token did not match.</response>
    [EndpointName("api/resource-servers/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        var settings = resourceServer?.Settings;
        if (settings is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            settings,
            Operations.Update
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, settings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(settings.Value);

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

        settings.Value = SerializeToElement(jsonObject);
        await store.UpdateSettingsAsync(settings, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = settings.ConcurrencyToken;
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a resource server's effective (merged) settings.
    /// </summary>
    /// <response code="200">The resource server's effective settings.</response>
    [EndpointName("api/resource-servers/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var effectiveSettings = await MergeResourceServerSettingsAsync(
            openIdContext,
            tenantId,
            resourceServer,
            storeManager,
            cancellationToken
        );
        if (effectiveSettings is null)
        {
            return TypedResults.NotFound();
        }

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            effectiveSettings,
            node,
            Operations.Read,
            settings => ToResourceServerEffectiveSettingsResource(resourceServer, settings)
        );
    }

    /// <summary>
    /// Gets the collection of secrets owned by a resource server.
    /// </summary>
    /// <response code="200">The resource server's secrets.</response>
    [EndpointName("api/resource-servers/secrets/get")]
    internal virtual async ValueTask<IResult> GetSecretsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var secrets = await store.GetSecretsOrDefaultAsync(resourceServerId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            secrets,
            node,
            Operations.Read,
            ToResourceServerSecretsResource
        );
    }

    /// <summary>
    /// Creates a secret for a resource server. The generated key material is returned exactly once (ADR-0050).
    /// </summary>
    /// <response code="201">The created secret, including its one-time key material.</response>
    [EndpointName("api/resource-servers/secrets/create")]
    internal virtual async ValueTask<IResult> CreateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] CreateSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            resourceServer,
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

        GeneratedSecret generatedSecret;
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
            await store.AddSecretAsync(resourceServerId, generatedSecret.Secret, cancellationToken);
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

        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}",
            ToCreatedSecretResource(generatedSecret.Secret, generatedSecret.SecretMaterial)
        );
    }

    /// <summary>
    /// Gets a single secret owned by a resource server.
    /// </summary>
    /// <response code="200">The requested secret.</response>
    [EndpointName("api/resource-servers/secrets/get-one")]
    internal virtual async ValueTask<IResult> GetSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerSecret = await store.GetSecretOrDefaultAsync(
            resourceServerId,
            secretId,
            cancellationToken
        );

        var scoped = resourceServerSecret is null
            ? null
            : TenantOwnedResource.For(resourceServerSecret.TenantId, resourceServerSecret.Value);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            scoped,
            node,
            Operations.Read,
            x => ToSecretResource(x.Value)
        );
    }

    /// <summary>
    /// Gets the public key for a resource server's secret, or <c>null</c> for a symmetric secret.
    /// </summary>
    /// <response code="200">The requested public key.</response>
    [EndpointName("api/resource-servers/secrets/get-public-key")]
    internal virtual async ValueTask<IResult> GetPublicKeyAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerSecret = await store.GetSecretOrDefaultAsync(
            resourceServerId,
            secretId,
            cancellationToken
        );

        var scoped = resourceServerSecret is null
            ? null
            : TenantOwnedResource.For(resourceServerSecret.TenantId, resourceServerSecret.Value);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            scoped,
            node,
            Operations.Read,
            x => SecretPublicKeyConverter.Convert(x.Value)
        );
    }

    /// <summary>
    /// Updates the metadata of a resource server's secret. Key material is immutable.
    /// </summary>
    /// <response code="204">The secret was updated.</response>
    /// <response code="412">The supplied <c>If-Match</c> concurrency token did not match.</response>
    [EndpointName("api/resource-servers/secrets/update")]
    internal virtual async ValueTask<IResult> UpdateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        [FromBody] UpdateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerSecret = await store.GetSecretOrDefaultAsync(
            resourceServerId,
            secretId,
            cancellationToken
        );
        if (resourceServerSecret is null)
        {
            return TypedResults.NotFound();
        }

        var secret = resourceServerSecret.Value;

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(resourceServerSecret.TenantId, secret),
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

        await store.UpdateSecretAsync(resourceServerId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a resource server's secret.
    /// </summary>
    /// <response code="204">The secret was deleted.</response>
    [EndpointName("api/resource-servers/secrets/delete")]
    internal virtual async ValueTask<IResult> DeleteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerSecret = await store.GetSecretOrDefaultAsync(
            resourceServerId,
            secretId,
            cancellationToken
        );
        if (resourceServerSecret is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(resourceServerSecret.TenantId, resourceServerSecret.Value),
            Operations.Delete
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await store.RemoveSecretAsync(resourceServerId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
