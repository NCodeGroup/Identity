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
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Principals;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Principals;

/*

GET  api/tenants/{tenantId}/principals/{principalId}
POST api/tenants/{tenantId}/principals/{principalId}/block
POST api/tenants/{tenantId}/principals/{principalId}/unblock

*/

/// <summary>
/// Provides the API endpoints for administering an end user's federated principal: reading whether it is blocked, and
/// blocking or unblocking it whatever connections the end user signs in through. Blocking a principal ends every
/// session it holds, which revokes its grants and notifies its relying parties, in the addressed tenant's security
/// domain. Every operation authorizes against the owning tenant scope, as the sessions endpoints do.
/// </summary>
internal class PrincipalApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICryptoService cryptoService,
    ISessionService sessionService,
    IOpenIdContextFactory openIdContextFactory,
    IManagementAuditRecorder managementAuditRecorder
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
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
        var principals = endpoints
            .MapGroup("/principals")
            .WithTags(OpenIdConstants.EndpointTags.Principals);

        principals.MapGet("/{principalId}", GetPrincipalAsync).Produces<PrincipalResource>();
        principals
            .MapPost("/{principalId}/block", BlockPrincipalAsync)
            .Produces(StatusCodes.Status204NoContent);
        principals
            .MapPost("/{principalId}/unblock", UnblockPrincipalAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Maps a <see cref="PersistedFederatedPrincipal"/> to its <see cref="PrincipalResource"/> representation.
    /// </summary>
    /// <param name="principal">The <see cref="PersistedFederatedPrincipal"/> to map.</param>
    /// <returns>The mapped <see cref="PrincipalResource"/>.</returns>
    internal virtual PrincipalResource ToPrincipalResource(PersistedFederatedPrincipal principal) =>
        new()
        {
            TenantId = principal.TenantId,
            PrincipalId = principal.PrincipalId,
            IsBlocked = principal.IsBlocked,
            ConcurrencyToken = principal.ConcurrencyToken,
        };

    /// <summary>
    /// Gets an end user's federated principal, including whether an administrator blocked it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="principalId">The server-owned identifier of the end user.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The principal.</response>
    /// <response code="404">The principal does not exist in the tenant.</response>
    [EndpointName("api/principals/get")]
    internal virtual async ValueTask<IResult> GetPrincipalAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Read))
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var principal = await storeManager
            .GetStore<IFederatedPrincipalStore>()
            .GetOrDefaultAsync(principalId, cancellationToken);
        if (principal is null)
        {
            return TypedResults.NotFound();
        }

        httpContext.Response.Headers.ETag = principal.ConcurrencyToken;
        return TypedResults.Json(ToPrincipalResource(principal));
    }

    /// <summary>
    /// Blocks an end user, which ends every session of its principal and refuses every token request for it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="principalId">The server-owned identifier of the end user.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The principal is blocked and its sessions are ended.</response>
    /// <response code="404">The principal does not exist in the tenant.</response>
    [EndpointName("api/principals/block")]
    internal virtual async ValueTask<IResult> BlockPrincipalAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update))
        {
            return AuthorizationFailed(httpContext);
        }

        var principal = await SaveBlockStateAsync(principalId, isBlocked: true, cancellationToken);
        if (principal is null)
        {
            return TypedResults.NotFound();
        }

        // A blocked principal's sessions end, which revokes its grants and notifies its relying parties. The change is
        // committed, so a client disconnect does not cancel the termination. See ADR-0226.
        await TerminateSessionsAsync(
            httpContext,
            tenantId,
            principal.PrincipalId,
            SessionTerminationReasons.UserBlocked,
            CancellationToken.None
        );

        await RecordPrincipalChangedAsync(httpContext, tenantId, principal, cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Unblocks an end user, which lets it sign in and request tokens again.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="principalId">The server-owned identifier of the end user.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The principal is not blocked.</response>
    /// <response code="404">The principal does not exist in the tenant.</response>
    [EndpointName("api/principals/unblock")]
    internal virtual async ValueTask<IResult> UnblockPrincipalAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update))
        {
            return AuthorizationFailed(httpContext);
        }

        var principal = await SaveBlockStateAsync(principalId, isBlocked: false, cancellationToken);
        if (principal is null)
        {
            return TypedResults.NotFound();
        }

        await RecordPrincipalChangedAsync(httpContext, tenantId, principal, cancellationToken);

        return TypedResults.NoContent();
    }

    // Writes the block state when it changes and returns the principal, or null when the principal does not exist.
    private async ValueTask<PersistedFederatedPrincipal?> SaveBlockStateAsync(
        string principalId,
        bool isBlocked,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IFederatedPrincipalStore>();

        var principal = await store.GetOrDefaultAsync(principalId, cancellationToken);
        if (principal is null)
        {
            return null;
        }

        if (principal.IsBlocked != isBlocked)
        {
            principal.IsBlocked = isBlocked;
            await store.UpdateAsync(principal, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        return principal;
    }

    // Materializes a context for the addressed tenant so session termination runs the seam (grant revocation,
    // back-channel logout, auditing) in that tenant's security domain rather than the request's resolved tenant.
    private async ValueTask TerminateSessionsAsync(
        HttpContext httpContext,
        string tenantId,
        string principalId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        await using var tenantContext = await OpenIdContextFactory.CreateForTenantAsync(
            httpContext,
            tenantId,
            mediator,
            cancellationToken
        );

        await SessionService.RevokeAllAsync(tenantContext, principalId, reason, cancellationToken);
    }

    private ValueTask RecordPrincipalChangedAsync(
        HttpContext httpContext,
        string tenantId,
        PersistedFederatedPrincipal principal,
        CancellationToken cancellationToken
    ) =>
        ManagementAuditRecorder.RecordResourceChangeAsync(
            httpContext,
            new PrincipalChangedAuditEvent
            {
                TenantId = tenantId,
                SubjectId = principal.PrincipalId,
                ResourceId = principal.PrincipalId,
                ChangeType = ResourceChangeTypes.Updated,
                ResourceValues = ToResourceValues(ToPrincipalResource(principal)),
                Outcome = AuditOutcome.Success,
            },
            cancellationToken
        );

    private async ValueTask<bool> AuthorizeTenantAsync(
        HttpContext httpContext,
        string tenantId,
        IAuthorizationRequirement operation
    )
    {
        var result = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantScopeResource.For(tenantId),
            operation
        );
        return result.Succeeded;
    }
}
