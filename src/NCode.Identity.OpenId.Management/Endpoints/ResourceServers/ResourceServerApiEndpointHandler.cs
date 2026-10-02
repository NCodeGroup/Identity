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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.Identity.Endpoints;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/*

GET    api/resource-servers
POST   api/resource-servers
GET    api/resource-servers/{resourceServerId}
PATCH  api/resource-servers/{resourceServerId}
DELETE api/resource-servers/{resourceServerId}

GET    api/resource-servers/{resourceServerId}/scopes
POST   api/resource-servers/{resourceServerId}/scopes
GET    api/resource-servers/{resourceServerId}/scopes/{scopeValue}
PUT    api/resource-servers/{resourceServerId}/scopes/{scopeValue}
DELETE api/resource-servers/{resourceServerId}/scopes/{scopeValue}

*/

/// <summary>
/// Provides the API endpoints for managing resource servers (APIs, in Auth0 terms) and their scopes.
/// </summary>
internal class ResourceServerApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IResourceServerValidator resourceServerValidator,
    IAmbientTenantAccessor ambientTenantAccessor,
    ICryptoService cryptoService,
    IResourceOwnershipService resourceOwnershipService,
    ILogger<ResourceServerApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IManagementEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IResourceServerValidator ResourceServerValidator { get; } = resourceServerValidator;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

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
            .AddEndpointFilter<TenantScopeEndpointFilter>()
            .WithTags("ResourceServers");

        resourceServers
            .MapGet("", ListAsync)
            .Produces<CollectionResource<ResourceServerResource>>();
        resourceServers
            .MapPost("", CreateAsync)
            .Produces<ResourceServerResource>(StatusCodes.Status201Created);
        resourceServers.MapGet("/{resourceServerId}", GetAsync).Produces<ResourceServerResource>();
        resourceServers.MapPatch("/{resourceServerId}", UpdateAsync);
        resourceServers.MapDelete("/{resourceServerId}", DeleteAsync);

        resourceServers
            .MapGet("/{resourceServerId}/scopes", ListScopesAsync)
            .Produces<IReadOnlyList<ScopeResource>>();
        resourceServers
            .MapPost("/{resourceServerId}/scopes", CreateScopeAsync)
            .Produces<ScopeResource>(StatusCodes.Status201Created);
        resourceServers
            .MapGet("/{resourceServerId}/scopes/{scopeValue}", GetScopeAsync)
            .Produces<ScopeResource>();
        resourceServers.MapPut("/{resourceServerId}/scopes/{scopeValue}", UpdateScopeAsync);
        resourceServers.MapDelete("/{resourceServerId}/scopes/{scopeValue}", DeleteScopeAsync);

        resourceServers
            .MapGet("/{resourceServerId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        resourceServers
            .MapPost("/{resourceServerId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        resourceServers.MapDelete("/{resourceServerId}/owners/{principalId}", RemoveOwnerAsync);
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
            Settings = resourceServer.Settings,
            Scopes = resourceServer.Scopes.Select(ToScopeResource).ToList(),
        };

    /// <summary>
    /// Handles <c>GET api/resource-servers</c>, returning a page of resource servers in the request's tenant.
    /// </summary>
    [EndpointName("api/resource-servers/list")]
    internal virtual async ValueTask<IResult> ListAsync(
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var tenantId = AmbientTenantAccessor.GetRequiredTenantId();
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            new TenantScopeResource(tenantId),
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
    /// Handles <c>GET api/resource-servers/{resourceServerId}</c>.
    /// </summary>
    [EndpointName("api/resource-servers/get")]
    internal virtual async ValueTask<IResult> GetAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);

        var node = ResourceNode.For(
            AmbientTenantAccessor.GetRequiredTenantId(),
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );
        return await ProcessGetAsync(
            httpContext,
            resourceServer,
            node,
            Operations.Read,
            ToResourceServerResource
        );
    }

    /// <summary>
    /// Handles <c>POST api/resource-servers</c>, creating a new resource server.
    /// </summary>
    [EndpointName("api/resource-servers/create")]
    internal virtual async ValueTask<IResult> CreateAsync(
        HttpContext httpContext,
        [FromBody] CreateResourceServerRequest request,
        CancellationToken cancellationToken
    )
    {
        var tenantId = AmbientTenantAccessor.GetRequiredTenantId();

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
            Settings = request.Settings,
            Scopes = [],
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
            $"/resource-servers/{resourceServerId}",
            ToResourceServerResource(resourceServer)
        );
    }

    /// <summary>
    /// Handles <c>PATCH api/resource-servers/{resourceServerId}</c>.
    /// </summary>
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
            Settings = resourceServer.Settings,
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
        resourceServer.Settings = model.Settings;

        await store.UpdateAsync(resourceServer, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>DELETE api/resource-servers/{resourceServerId}</c>. System resource servers and resource servers
    /// still referenced by a client grant cannot be removed (<c>409 Conflict</c>).
    /// </summary>
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
    /// Handles <c>GET api/resource-servers/{resourceServerId}/scopes</c>.
    /// </summary>
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
    /// Handles <c>POST api/resource-servers/{resourceServerId}/scopes</c>.
    /// </summary>
    [EndpointName("api/resource-servers/scopes/create")]
    internal virtual async ValueTask<IResult> CreateScopeAsync(
        HttpContext httpContext,
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
            $"/resource-servers/{resourceServerId}/scopes/{request.Value}",
            ToScopeResource(persistedScope)
        );
    }

    /// <summary>
    /// Handles <c>GET api/resource-servers/{resourceServerId}/scopes/{scopeValue}</c>.
    /// </summary>
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
    /// Handles <c>PUT api/resource-servers/{resourceServerId}/scopes/{scopeValue}</c>.
    /// </summary>
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
    /// Handles <c>DELETE api/resource-servers/{resourceServerId}/scopes/{scopeValue}</c>. A system scope cannot be
    /// removed (<c>409 Conflict</c>).
    /// </summary>
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
    /// Handles <c>GET api/resource-servers/{resourceServerId}/owners</c>, returning the resource server's owners.
    /// </summary>
    [EndpointName("api/resource-servers/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var tenantId = AmbientTenantAccessor.GetRequiredTenantId();
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
    /// Handles <c>POST api/resource-servers/{resourceServerId}/owners</c>, granting a principal ownership.
    /// </summary>
    [EndpointName("api/resource-servers/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    )
    {
        var tenantId = AmbientTenantAccessor.GetRequiredTenantId();
        return await ProcessAddOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId,
            request,
            $"/resource-servers/{resourceServerId}/owners",
            (storeManager, token) =>
                ResourceServerExistsAsync(storeManager, resourceServerId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Handles <c>DELETE api/resource-servers/{resourceServerId}/owners/{principalId}</c>, revoking ownership. Refused
    /// when it would leave the resource server with no owner.
    /// </summary>
    [EndpointName("api/resource-servers/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string resourceServerId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        var tenantId = AmbientTenantAccessor.GetRequiredTenantId();
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
}
