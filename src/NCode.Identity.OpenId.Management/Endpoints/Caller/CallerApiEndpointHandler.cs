// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Caller;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Caller;

/*

GET api/me
GET api/me/teams
GET api/me/tenants

*/

/// <summary>
/// Provides the API endpoints that tell the caller of the management API where it holds authority: the principal it
/// acts as, whether it administers the platform, the role assignments it holds, and the teams and tenants it holds a
/// role at. Authority stays per resource and the token names no team, so every answer is read from the caller's role
/// assignments at the time of the request.
/// </summary>
// See ADR-1420
internal class CallerApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICallerPrincipalResolver callerPrincipalResolver,
    IAmbientTenantAccessor ambientTenantAccessor
) : IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAuthorizationService AuthorizationService { get; } = authorizationService;
    private ICallerPrincipalResolver CallerPrincipalResolver { get; } = callerPrincipalResolver;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/me").WithTags(OpenIdConstants.EndpointTags.Caller);

        me.MapGet("", GetCallerAsync).Produces<CallerResource>();
        me.MapGet("/teams", ListTeamsAsync).Produces<CollectionResource<TeamResource>>();
        me.MapGet("/tenants", ListTenantsAsync).Produces<CollectionResource<TenantResource>>();
    }

    /// <summary>
    /// Maps a <see cref="PersistedRoleAssignment"/> to its <see cref="RoleAssignmentResource"/> representation.
    /// </summary>
    /// <param name="assignment">The <see cref="PersistedRoleAssignment"/> to map.</param>
    /// <returns>The mapped <see cref="RoleAssignmentResource"/>.</returns>
    internal virtual RoleAssignmentResource ToRoleAssignmentResource(
        PersistedRoleAssignment assignment
    ) =>
        new()
        {
            AssignmentId = assignment.AssignmentId,
            PrincipalId = assignment.PrincipalId,
            RoleName = assignment.RoleName,
            ResourceType = assignment.ResourceType,
            ResourceId = assignment.ResourceId,
            TenantId = assignment.TenantId,
        };

    /// <summary>
    /// Maps a <see cref="PersistedTeam"/> to its <see cref="TeamResource"/> representation.
    /// </summary>
    /// <param name="team">The <see cref="PersistedTeam"/> to map.</param>
    /// <returns>The mapped <see cref="TeamResource"/>.</returns>
    internal virtual TeamResource ToTeamResource(PersistedTeam team) =>
        new()
        {
            TeamId = team.TeamId,
            ConcurrencyToken = team.ConcurrencyToken,
            DisplayName = team.DisplayName,
            IsDisabled = team.IsDisabled,
        };

    /// <summary>
    /// Maps a <see cref="PersistedTenant"/> to its <see cref="TenantResource"/> representation without its environment.
    /// </summary>
    /// <param name="tenant">The <see cref="PersistedTenant"/> to map.</param>
    /// <returns>The mapped <see cref="TenantResource"/>.</returns>
    internal virtual TenantResource ToTenantResource(PersistedTenant tenant) =>
        new()
        {
            TenantId = tenant.TenantId,
            TeamId = tenant.TeamId,
            ConcurrencyToken = tenant.ConcurrencyToken,
            Issuer = tenant.Issuer,
            MtlsEndpointAliasAuthority = tenant.MtlsEndpointAliasAuthority,
            IsDisabled = tenant.IsDisabled,
            DisplayName = tenant.DisplayName,
            Environment = null,
        };

    /// <summary>
    /// Gets where the caller holds authority: the principal it acts as, whether it holds authority at the platform
    /// node, and the role assignments it holds.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The caller's principal, platform authority and role assignments.</response>
    [EndpointName("api/me/get")]
    internal virtual async ValueTask<IResult> GetCallerAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var (principalId, assignments) = await GetAssignmentsAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            cancellationToken
        );

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            ResourceNode.For(
                openIdContext.Tenant.TenantId,
                ResourceNodeTypes.Platform,
                OpenIdConstants.PlatformResourceId
            ),
            Operations.Read
        );

        return TypedResults.Json(
            new CallerResource
            {
                PrincipalId = principalId,
                IsPlatformAdministrator = authorizationResult.Succeeded,
                Assignments = assignments.Select(ToRoleAssignmentResource).ToList(),
            }
        );
    }

    /// <summary>
    /// Lists the teams the caller holds a role assignment at.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The teams the caller holds a role at, ordered by identifier.</response>
    [EndpointName("api/me/teams/list")]
    internal virtual async ValueTask<IResult> ListTeamsAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var (_, assignments) = await GetAssignmentsAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            cancellationToken
        );

        var teams = new List<TeamResource>();
        var store = storeManager.GetStore<ITeamStore>();
        foreach (var teamId in GetResourceIds(assignments, ResourceNodeTypes.Team))
        {
            var team = await store.GetOrDefaultAsync(teamId, cancellationToken);
            if (team is not null)
            {
                teams.Add(ToTeamResource(team));
            }
        }

        return TypedResults.Json(
            new CollectionResource<TeamResource> { Items = teams, ContinuationToken = null }
        );
    }

    /// <summary>
    /// Lists the tenants the caller holds a role assignment at.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenants the caller holds a role at, ordered by identifier.</response>
    [EndpointName("api/me/tenants/list")]
    internal virtual async ValueTask<IResult> ListTenantsAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var (_, assignments) = await GetAssignmentsAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            cancellationToken
        );

        var tenants = new List<TenantResource>();
        var store = storeManager.GetStore<ITenantStore>();
        foreach (var tenantId in GetResourceIds(assignments, ResourceNodeTypes.Tenant))
        {
            var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
            if (tenant is not null)
            {
                tenants.Add(ToTenantResource(tenant));
            }
        }

        return TypedResults.Json(
            new CollectionResource<TenantResource> { Items = tenants, ContinuationToken = null }
        );
    }

    // The caller's principal and the role assignments it holds; a caller that resolves to no principal holds none.
    private async ValueTask<(
        string? PrincipalId,
        IReadOnlyList<PersistedRoleAssignment> Assignments
    )> GetAssignmentsAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var principalId = await CallerPrincipalResolver.ResolveCallerPrincipalIdOrDefaultAsync(
            openIdContext,
            user,
            storeManager,
            cancellationToken
        );
        if (string.IsNullOrEmpty(principalId))
        {
            return (null, []);
        }

        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignments = await store.GetByPrincipalAsync(principalId, cancellationToken);
        return (principalId, assignments);
    }

    // The distinct identifiers of the nodes of one type the assignments are held at, ordered ordinally.
    private static IEnumerable<string> GetResourceIds(
        IEnumerable<PersistedRoleAssignment> assignments,
        string resourceType
    ) =>
        assignments
            .Where(assignment =>
                string.Equals(assignment.ResourceType, resourceType, StringComparison.Ordinal)
            )
            .Select(assignment => assignment.ResourceId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal);
}
