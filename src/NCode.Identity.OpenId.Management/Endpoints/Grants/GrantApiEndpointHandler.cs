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
using NCode.Identity.Events.Audit;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
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

POST   api/tenants/{tenantId}/grants/initial-access-tokens

*/

/// <summary>
/// Provides the API endpoints for administering persisted OpenID grants: listing, retrieving, and
/// revoking grants, and minting initial access tokens for protected dynamic client registration. Grants are
/// addressed by their opaque <c>GrantId</c>; the internal <c>HashedKey</c> is never exposed.
/// </summary>
internal class GrantApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IManagementAuditRecorder managementAuditRecorder,
    IPersistedGrantService persistedGrantService,
    TimeProvider timeProvider,
    ICryptoService cryptoService
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
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
        grants
            .MapPost("/initial-access-tokens", CreateInitialAccessTokenAsync)
            .Produces<CreatedInitialAccessTokenResource>(StatusCodes.Status201Created)
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
            SessionId = grant.SessionId,
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
    /// <param name="sessionId">When specified, restricts the page to grants issued under this session.</param>
    /// <param name="grantType">When specified, restricts the page to grants of this type.</param>
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
        [FromQuery] string? sessionId,
        [FromQuery] string? grantType,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        // A grant list is scoped to the addressed tenant (the query filter enforces it); authorize for that tenant.
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IGrantStore>();
                return await store.GetPageAsync(
                    new GrantFilter
                    {
                        SubjectId = subjectId,
                        ClientId = clientId,
                        SessionId = sessionId,
                        GrantType = grantType,
                    },
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToGrantResource
        );
    }

    /// <summary>
    /// Bulk-revokes a tenant's grants, filtered by subject, client, and/or session. At least one filter is required.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="subjectId">When specified, restricts the revocation to grants for this subject.</param>
    /// <param name="clientId">When specified, restricts the revocation to grants for this client.</param>
    /// <param name="sessionId">When specified, restricts the revocation to grants issued under this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The number of grants that were revoked.</response>
    /// <response code="400">Neither a subject, a client, nor a session filter was supplied.</response>
    [EndpointName("api/grants/revoke-many")]
    internal virtual async ValueTask<IResult> RevokeGrantsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? subjectId,
        [FromQuery] string? clientId,
        [FromQuery] string? sessionId,
        CancellationToken cancellationToken
    )
    {
        // Require at least one filter: an unfiltered bulk revoke would silently revoke the whole tenant.
        var filter = new GrantFilter
        {
            SubjectId = subjectId,
            ClientId = clientId,
            SessionId = sessionId,
        };
        if (filter.IsEmpty)
        {
            return TypedResults.Problem(
                detail: "At least one of 'subjectId', 'clientId', or 'sessionId' must be specified.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantScopeResource.For(tenantId),
            Operations.Delete
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var revoked = await store.RevokeWhereAsync(
            filter,
            TimeProvider.GetUtcNow(),
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordGrantsRevokedAsync(
            httpContext,
            tenantId,
            subjectId,
            clientId,
            sessionId,
            revoked,
            cancellationToken
        );

        return TypedResults.Json(new GrantRevocationResult { Revoked = revoked });
    }

    /// <summary>
    /// Mints an initial access token (RFC 7591) that authorizes its bearer to register a new client at the protected
    /// dynamic client registration endpoint. The token is persisted as a hash-keyed <c>initial_access_token</c> grant,
    /// so its plaintext value is revealed exactly once in the response and never retrievable again.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that will own the token.</param>
    /// <param name="request">The token-creation request, carrying an optional description and lifetime.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The newly minted initial access token, including its one-time plaintext value.</response>
    /// <response code="400">The requested lifetime was not greater than zero.</response>
    [EndpointName("api/grants/initial-access-tokens/create")]
    internal virtual async ValueTask<IResult> CreateInitialAccessTokenAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateInitialAccessTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        TimeSpan? lifetime = null;
        if (request.LifetimeSeconds is { } lifetimeSeconds)
        {
            if (lifetimeSeconds <= 0)
            {
                return TypedResults.Problem(
                    detail: "'lifetimeSeconds' must be greater than zero.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            lifetime = TimeSpan.FromSeconds(lifetimeSeconds);
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantScopeResource.For(tenantId),
            Operations.Create
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var createdWhen = TimeProvider.GetUtcNow();
        var token = CryptoService.GenerateUrlSafeKey();

        var grant = new PersistedGrant<InitialAccessTokenGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = tenantId,
            ClientId = null,
            SubjectId = null,
            SessionId = null,
            Payload = new InitialAccessTokenGrant { Description = request.Description },
        };

        var grantId = await PersistedGrantService.AddAsync(
            openIdContext,
            PersistedGrantService.CreateGrantId(
                tenantId,
                OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
                token
            ),
            grant,
            createdWhen,
            lifetime,
            cancellationToken
        );

        await ManagementAuditRecorder.RecordResourceChangeAsync(
            httpContext,
            new InitialAccessTokenCreatedAuditEvent
            {
                TenantId = tenantId,
                ResourceId = grantId,
                ChangeType = ResourceChangeTypes.Created,
                Outcome = AuditOutcome.Success,
            },
            cancellationToken
        );

        var response = new CreatedInitialAccessTokenResource
        {
            InitialAccessToken = token,
            GrantId = grantId,
            TenantId = tenantId,
            Description = request.Description,
            CreatedWhen = createdWhen,
            ExpiresWhen = lifetime is null ? null : createdWhen + lifetime,
        };

        return TypedResults.Json(response, statusCode: StatusCodes.Status201Created);
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
            TenantScopeResource.For(tenantId),
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
            TenantScopeResource.For(tenantId),
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

            await ManagementAuditRecorder.RecordGrantRevokedAsync(
                httpContext,
                tenantId,
                grant.SubjectId,
                grant.ClientId,
                grant.GrantType,
                cancellationToken
            );
        }

        return TypedResults.NoContent();
    }
}
