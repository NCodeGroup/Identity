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
using NCode.Identity.Endpoints;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/*

GET    api/tenants/{tenantId}/clients/{clientId}/grants
POST   api/tenants/{tenantId}/clients/{clientId}/grants
GET    api/tenants/{tenantId}/clients/{clientId}/grants/{resourceServerId}
PUT    api/tenants/{tenantId}/clients/{clientId}/grants/{resourceServerId}
DELETE api/tenants/{tenantId}/clients/{clientId}/grants/{resourceServerId}

*/

/// <summary>
/// Provides the API endpoints for managing a client's grants (in Auth0 terms): its authorizations to resource servers
/// with granted scopes. Nested under the client so the required relationship is a path, not a query parameter.
/// </summary>
internal class ClientGrantApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IClientGrantValidator clientGrantValidator,
    ICryptoService cryptoService
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IClientGrantValidator ClientGrantValidator { get; } = clientGrantValidator;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var grants = endpoints.MapGroup("/grants").WithTags("ClientGrants");

        grants.MapGet("", ListAsync).Produces<CollectionResource<ClientGrantResource>>();
        grants
            .MapPost("", CreateAsync)
            .Produces<ClientGrantResource>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status422UnprocessableEntity);
        grants.MapGet("/{resourceServerId}", GetAsync).Produces<ClientGrantResource>();
        grants
            .MapPut("/{resourceServerId}", UpdateAsync)
            .Produces(StatusCodes.Status422UnprocessableEntity);
        grants.MapDelete("/{resourceServerId}", DeleteAsync);
    }

    internal virtual ClientGrantResource ToResource(PersistedClientGrant grant) =>
        new()
        {
            TenantId = grant.TenantId,
            ClientId = grant.ClientId,
            ResourceServerId = grant.ResourceServerId,
            ConcurrencyToken = grant.ConcurrencyToken,
            Scopes = grant.Scopes,
        };

    /// <summary>
    /// Handles <c>GET api/clients/{clientId}/grants</c>.
    /// </summary>
    [EndpointName("api/clients/grants/list")]
    internal virtual async ValueTask<IResult> ListAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            new TenantScopeResource(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IClientGrantStore>();
                return await store.GetPageAsync(
                    clientId,
                    cursor,
                    effectiveLimit,
                    cancellationToken
                );
            },
            ToResource
        );
    }

    /// <summary>
    /// Handles <c>GET api/clients/{clientId}/grants/{resourceServerId}</c>.
    /// </summary>
    [EndpointName("api/clients/grants/get")]
    internal virtual async ValueTask<IResult> GetAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientGrantStore>();

        var grant = await store.GetOrDefaultAsync(clientId, resourceServerId, cancellationToken);

        return await ProcessGetAsync(httpContext, grant, Operations.Read, ToResource);
    }

    /// <summary>
    /// Handles <c>POST api/clients/{clientId}/grants</c>, authorizing a client to a resource server with scopes.
    /// </summary>
    [EndpointName("api/clients/grants/create")]
    internal virtual async ValueTask<IResult> CreateAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string clientId,
        [FromBody] CreateClientGrantRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var grantStore = storeManager.GetStore<IClientGrantStore>();

        var grant = new PersistedClientGrant
        {
            TenantId = tenantId,
            ClientId = clientId,
            ResourceServerId = request.ResourceServerId,
            ConcurrencyToken = string.Empty,
            Scopes = request.Scopes,
        };

        // Core preconditions (authorization + resource-server existence + scope-subset + uniqueness) are asserted by
        // the validator (ADR-0014).
        var error = await ClientGrantValidator.ValidateCreateAsync(
            httpContext.User,
            grant,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        await grantStore.AddAsync(grant, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = grant.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/clients/{clientId}/grants/{request.ResourceServerId}",
            ToResource(grant)
        );
    }

    /// <summary>
    /// Handles <c>PUT api/clients/{clientId}/grants/{resourceServerId}</c>, replacing the granted scopes.
    /// </summary>
    [EndpointName("api/clients/grants/update")]
    internal virtual async ValueTask<IResult> UpdateAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        [FromRoute] string resourceServerId,
        [FromBody] UpdateClientGrantRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var grantStore = storeManager.GetStore<IClientGrantStore>();

        var grant = await grantStore.GetOrDefaultAsync(
            clientId,
            resourceServerId,
            cancellationToken
        );
        if (grant is null)
        {
            return TypedResults.NotFound();
        }

        // Core preconditions (authorization + resource-server existence + scope-subset) are asserted by the
        // validator (ADR-0014).
        var error = await ClientGrantValidator.ValidateUpdateAsync(
            httpContext.User,
            grant,
            request.Scopes,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        grant.Scopes = request.Scopes;

        await grantStore.UpdateAsync(grant, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>DELETE api/clients/{clientId}/grants/{resourceServerId}</c>, revoking the client's authorization.
    /// </summary>
    [EndpointName("api/clients/grants/delete")]
    internal virtual async ValueTask<IResult> DeleteAsync(
        HttpContext httpContext,
        [FromRoute] string clientId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientGrantStore>();

        var grant = await store.GetOrDefaultAsync(clientId, resourceServerId, cancellationToken);
        if (grant is null)
        {
            return TypedResults.NotFound();
        }

        var error = await ClientGrantValidator.ValidateDeleteAsync(
            httpContext.User,
            grant,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        await store.RemoveAsync(clientId, resourceServerId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
