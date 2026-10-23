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
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Sessions;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Sessions;

/*

GET    api/tenants/{tenantId}/sessions?subjectId={subjectId}
DELETE api/tenants/{tenantId}/sessions?subjectId={subjectId}
DELETE api/tenants/{tenantId}/sessions/{sessionId}

*/

/// <summary>
/// Provides the API endpoints for administering end-user sessions: listing a subject's active sessions and revoking a
/// single session or every session a subject holds. Unlike bulk grant revocation, every revocation here runs the full
/// session-termination seam (grant revocation, back-channel logout notification, and session auditing) in the addressed
/// tenant's security domain.
/// </summary>
internal class SessionApiEndpointHandler(
    IAuthorizationService authorizationService,
    ICryptoService cryptoService,
    ISessionService sessionService,
    IOpenIdContextFactory openIdContextFactory,
    IManagementAuditRecorder managementAuditRecorder
) : BaseApiEndpointHandler, IEndpointProvider
{
    private ISessionService SessionService { get; } = sessionService;
    private IOpenIdContextFactory OpenIdContextFactory { get; } = openIdContextFactory;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var sessions = endpoints
            .MapGroup("/sessions")
            .WithTags(OpenIdConstants.EndpointTags.Sessions);

        sessions.MapGet("", ListSessionsAsync).Produces<CollectionResource<SessionResource>>();
        sessions
            .MapDelete("", RevokeSessionsAsync)
            .Produces<SessionRevocationResult>()
            .Produces(StatusCodes.Status400BadRequest);
        sessions
            .MapDelete("/{sessionId}", RevokeSessionAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Maps a <see cref="SessionInfo"/> to its <see cref="SessionResource"/> representation.
    /// </summary>
    /// <param name="session">The <see cref="SessionInfo"/> to map.</param>
    /// <returns>The mapped <see cref="SessionResource"/>.</returns>
    internal virtual SessionResource ToSessionResource(SessionInfo session)
    {
        var grant = session.Grant;
        return new SessionResource
        {
            SessionId = session.SessionId,
            SubjectId = session.SubjectId,
            CreatedWhen = session.CreatedWhen,
            LastActivityWhen = grant.LastActivityWhen,
            ExpiresWhen = session.ExpiresWhen,
            AuthTime = grant.AuthTime,
            AuthenticationMethods = grant.AuthenticationMethods ?? [],
            AuthenticationContextClassReference = grant.AuthenticationContextClassReference,
            IdentityProvider = grant.IdentityProvider,
            ClientIds = grant.ClientIds,
            UserAgent = grant.UserAgent,
            RemoteIpAddress = grant.RemoteIpAddress,
        };
    }

    /// <summary>
    /// Lists a subject's active sessions in a tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="subjectId">The subject whose active sessions are listed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The subject's active sessions.</response>
    /// <response code="400">No subject was supplied.</response>
    [EndpointName("api/sessions/list")]
    internal virtual async ValueTask<IResult> ListSessionsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? subjectId,
        CancellationToken cancellationToken
    )
    {
        // Sessions are enumerated per subject; a subject is required (an unscoped list is not meaningful here).
        if (string.IsNullOrEmpty(subjectId))
        {
            return TypedResults.Problem(
                detail: "'subjectId' must be specified.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantScopeResource.For(tenantId),
            Operations.List
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        // Listing reads session grants constrained by the route's ambient tenant scope; the request's published context
        // (its environment) is sufficient, so a full addressed-tenant context is not materialized for a read.
        var openIdContext = httpContext.GetOpenIdContext();
        var sessions = await SessionService.GetSessionsAsync(
            openIdContext,
            subjectId,
            cancellationToken
        );

        var items = sessions.Select(ToSessionResource).ToList();
        return TypedResults.Json(
            new CollectionResource<SessionResource> { Items = items, ContinuationToken = null }
        );
    }

    /// <summary>
    /// Revokes every session a subject holds in a tenant, running the session-termination seam for each.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="subjectId">The subject whose sessions are revoked.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The number of sessions this request terminated.</response>
    /// <response code="400">No subject was supplied.</response>
    [EndpointName("api/sessions/revoke-many")]
    internal virtual async ValueTask<IResult> RevokeSessionsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? subjectId,
        CancellationToken cancellationToken
    )
    {
        // Require a subject: an unfiltered bulk session revoke would silently sign out the whole tenant.
        if (string.IsNullOrEmpty(subjectId))
        {
            return TypedResults.Problem(
                detail: "'subjectId' must be specified.",
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

        await using var tenantContext = await CreateTenantContextAsync(
            httpContext,
            tenantId,
            cancellationToken
        );

        var revoked = await SessionService.RevokeAllAsync(
            tenantContext,
            subjectId,
            SessionTerminationReasons.AdministrativeRevocation,
            cancellationToken
        );

        await ManagementAuditRecorder.RecordSessionsRevokedAsync(
            httpContext,
            tenantId,
            subjectId,
            sessionId: null,
            revoked,
            cancellationToken
        );

        return TypedResults.Json(new SessionRevocationResult { Revoked = revoked });
    }

    /// <summary>
    /// Revokes a single session by its <c>sid</c>, running the session-termination seam.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>) to revoke.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The session was revoked (or did not exist).</response>
    [EndpointName("api/sessions/revoke")]
    internal virtual async ValueTask<IResult> RevokeSessionAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string sessionId,
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

        await using var tenantContext = await CreateTenantContextAsync(
            httpContext,
            tenantId,
            cancellationToken
        );

        // Resolve the session (within the addressed tenant) so the audit records the subject, and a non-existent sid is
        // reported as revoked without dispatching the seam.
        var session = await SessionService.GetOrDefaultAsync(
            tenantContext,
            sessionId,
            cancellationToken
        );
        if (session is not { } found)
        {
            return TypedResults.NoContent();
        }

        var terminated = await SessionService.RevokeAsync(
            tenantContext,
            sessionId,
            SessionTerminationReasons.AdministrativeRevocation,
            cancellationToken
        );

        await ManagementAuditRecorder.RecordSessionsRevokedAsync(
            httpContext,
            tenantId,
            found.SubjectId,
            sessionId,
            revokedCount: terminated ? 1 : 0,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    // Materializes a context for the addressed tenant so session termination runs the seam (grant revocation,
    // back-channel logout, auditing) in that tenant's security domain rather than the request's resolved tenant.
    private ValueTask<OpenIdContext> CreateTenantContextAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return OpenIdContextFactory.CreateForTenantAsync(
            httpContext,
            tenantId,
            mediator,
            cancellationToken
        );
    }
}
