// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Subject;
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
    IPostCommitSecurityEffects postCommitSecurityEffects,
    IOpenIdContextFactory openIdContextFactory,
    IManagementAuditRecorder managementAuditRecorder
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPostCommitSecurityEffects PostCommitSecurityEffects { get; } =
        postCommitSecurityEffects;
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

        principals
            .MapGet("/{principalId}", GetPrincipalAsync)
            .Produces<PrincipalResource>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        principals
            .MapPost("/{principalId}/block", BlockPrincipalAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        principals
            .MapPost("/{principalId}/unblock", UnblockPrincipalAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
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
        if (!await AuthorizeTenantAsync(httpContext, tenantId, Operations.Read, cancellationToken))
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var principal = await storeManager
            .GetStore<IFederatedPrincipalStore>()
            .GetOrDefaultAsync(principalId, cancellationToken);
        if (principal is null)
        {
            return ManagementProblems.ResourceNotFound();
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
        if (
            !await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update, cancellationToken)
        )
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IFederatedPrincipalStore>();

        var principal = await store.GetOrDefaultAsync(principalId, cancellationToken);
        if (principal is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        // The tenant's context is created before the block is written: a tenant that is not served (disabled, or of a
        // disabled team) refuses the request here, before anything is committed or audited.
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        await using var tenantContext = await OpenIdContextFactory.CreateForTenantAsync(
            httpContext,
            tenantId,
            mediator,
            cancellationToken
        );

        if (!principal.IsBlocked)
        {
            principal.IsBlocked = true;
            await store.UpdateAsync(principal, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        // A blocked principal's sessions end, which revokes its grants and notifies its relying parties. The change is
        // committed, so the termination runs on its own bounded token and a failure is logged; the audit record
        // follows. See ADR-1226 and ADR-1468.
        await PostCommitSecurityEffects.TerminateSessionsAsync(
            tenantContext,
            principal.PrincipalId,
            SessionTerminationReasons.UserBlocked
        );

        await RecordPrincipalChangedAsync(httpContext, tenantId, principal);

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
        if (
            !await AuthorizeTenantAsync(httpContext, tenantId, Operations.Update, cancellationToken)
        )
        {
            return AuthorizationFailed(httpContext);
        }

        var principal = await SaveBlockStateAsync(principalId, isBlocked: false, cancellationToken);
        if (principal is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        await RecordPrincipalChangedAsync(httpContext, tenantId, principal);

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

    [SuppressMessage(
        "Design",
        "NCID0003",
        Justification = "The work follows a committed change and runs on its own bounded token through the audit recorder, so no caller can pass a token that drops it (ADR-1351, ADR-1464, ADR-1468)."
    )]
    private ValueTask RecordPrincipalChangedAsync(
        HttpContext httpContext,
        string tenantId,
        PersistedFederatedPrincipal principal
    ) =>
        ManagementAuditRecorder.RecordResourceChangeAsync(
            httpContext,
            new PrincipalChangedAuditEvent
            {
                EventSource = AuditEventSources.Management,
                TenantId = tenantId,
                SubjectId = principal.PrincipalId,
                ResourceId = principal.PrincipalId,
                ChangeType = ResourceChangeTypes.Updated,
                ResourceValues = ToResourceValues(ToPrincipalResource(principal)),
                Outcome = AuditOutcome.Success,
            }
        );

    private async ValueTask<bool> AuthorizeTenantAsync(
        HttpContext httpContext,
        string tenantId,
        IAuthorizationRequirement operation,
        CancellationToken cancellationToken
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
