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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Grants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Grants;

/*

GET    api/tenants/{tenantId}/grants
GET    api/tenants/{tenantId}/grants/{grantId}
DELETE api/tenants/{tenantId}/grants/{grantId}

*/

/// <summary>
/// Provides the read-only API endpoints for administering persisted OpenID grants: listing, retrieving, and
/// revoking. Grants are addressed by their opaque <c>GrantId</c>; the internal <c>HashedKey</c> is never exposed.
/// </summary>
internal class GrantApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    TimeProvider timeProvider,
    ICryptoService cryptoService
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var grants = endpoints.MapGroup("/grants").WithTags(OpenIdConstants.EndpointTags.Grants);

        grants.MapGet("", ListGrantsAsync).Produces<CollectionResource<GrantResource>>();
        grants
            .MapDelete("", RevokeGrantsAsync)
            .Produces<GrantRevocationResult>()
            .Produces(StatusCodes.Status400BadRequest);
        grants.MapGet("/{grantId}", GetGrantAsync).Produces<GrantResource>();
        grants.MapDelete("/{grantId}", RevokeGrantAsync).Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Maps a <see cref="PersistedGrant"/> to its metadata-only <see cref="GrantResource"/> representation.
    /// </summary>
    /// <param name="grant">The <see cref="PersistedGrant"/> to map.</param>
    /// <returns>The mapped <see cref="GrantResource"/>.</returns>
    internal virtual GrantResource ToGrantResource(PersistedGrant grant)
    {
        return new GrantResource
        {
            GrantId = grant.GrantId,
            GrantType = grant.GrantType,
            TenantId = grant.TenantId,
            ClientId = grant.ClientId,
            SubjectId = grant.SubjectId,
            CreatedWhen = grant.CreatedWhen,
            ExpiresWhen = grant.ExpiresWhen,
            RevokedWhen = grant.RevokedWhen,
            ConsumedWhen = grant.ConsumedWhen,
        };
    }

    /// <summary>
    /// Lists the grants in a tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="subjectId">When specified, restricts the page to grants for this subject.</param>
    /// <param name="clientId">When specified, restricts the page to grants for this client.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of grants to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of grants.</response>
    [EndpointName("api/grants/list")]
    internal virtual async ValueTask<IResult> ListGrantsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? subjectId,
        [FromQuery] string? clientId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        // A grant list is scoped to the addressed tenant (the query filter enforces it); authorize for that tenant.
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            new TenantScopeResource(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IGrantStore>();
                return await store.GetPageAsync(
                    subjectId,
                    clientId,
                    cursor,
                    effectiveLimit,
                    cancellationToken
                );
            },
            ToGrantResource
        );
    }

    /// <summary>
    /// Bulk-revokes a tenant's grants, filtered by subject and/or client. At least one filter is required.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="subjectId">When specified, restricts the revocation to grants for this subject.</param>
    /// <param name="clientId">When specified, restricts the revocation to grants for this client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The number of grants that were revoked.</response>
    /// <response code="400">Neither a subject nor a client filter was supplied.</response>
    [EndpointName("api/grants/revoke-many")]
    internal virtual async ValueTask<IResult> RevokeGrantsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? subjectId,
        [FromQuery] string? clientId,
        CancellationToken cancellationToken
    )
    {
        // Require at least one filter: an unfiltered bulk revoke would silently revoke the whole tenant.
        if (string.IsNullOrEmpty(subjectId) && string.IsNullOrEmpty(clientId))
        {
            return TypedResults.Problem(
                detail: "At least one of 'subjectId' or 'clientId' must be specified.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            new TenantScopeResource(tenantId),
            Operations.Delete
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var revoked = await store.RevokeWhereAsync(
            subjectId,
            clientId,
            TimeProvider.GetUtcNow(),
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.Json(new GrantRevocationResult { Revoked = revoked });
    }

    /// <summary>
    /// Gets a grant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="grantId">The opaque identifier of the grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested grant.</response>
    [EndpointName("api/grants/get")]
    internal virtual async ValueTask<IResult> GetGrantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string grantId,
        CancellationToken cancellationToken
    )
    {
        // Grants carry an optional tenant, so authorization is evaluated against the addressed tenant scope (the
        // query filter guarantees a returned grant belongs to that tenant) rather than the grant instance.
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            new TenantScopeResource(tenantId),
            Operations.Read
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var grant = await store.GetOrDefaultAsync(grantId, cancellationToken);
        if (grant is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Json(ToGrantResource(grant));
    }

    /// <summary>
    /// Revokes a grant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="grantId">The opaque identifier of the grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The grant was revoked.</response>
    [EndpointName("api/grants/revoke")]
    internal virtual async ValueTask<IResult> RevokeGrantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string grantId,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            new TenantScopeResource(tenantId),
            Operations.Delete
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var grant = await store.GetOrDefaultAsync(grantId, cancellationToken);
        if (grant is null)
        {
            return TypedResults.NotFound();
        }

        // Soft-revoke is idempotent: an already-revoked grant is left untouched.
        if (grant.RevokedWhen is null)
        {
            grant.RevokedWhen = TimeProvider.GetUtcNow();

            await store.UpdateAsync(grant, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }
}
