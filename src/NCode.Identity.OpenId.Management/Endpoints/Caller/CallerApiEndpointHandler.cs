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
using NCode.Identity.OpenId.Management.Endpoints.Teams;
using NCode.Identity.OpenId.Management.Endpoints.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Registration.AspNetCore;

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
/// assignments at the time of the request. It answers only a caller that the management authentication filter
/// authenticated from a management access token, which it reads from <see cref="IManagementCallerFeature"/>; any other
/// request gets the authentication-required problem.
/// </summary>
// See ADR-1420 and ADR-1044.
internal class CallerApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IPrincipalResolver principalResolver,
    IAmbientTenantAccessor ambientTenantAccessor
) : IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAuthorizationService AuthorizationService { get; } = authorizationService;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var me = endpoints
            .MapGroup("/me")
            .WithTags(OpenIdConstants.EndpointTags.Caller)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

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
        return await AnswerCallerAsync(httpContext, DescribeCallerAsync, cancellationToken);
    }

    /// <summary>
    /// Describes the caller's principal, its authority at the platform node and the role assignments it holds.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="caller">The <see cref="ClaimsPrincipal"/> of the caller.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> to read through.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the JSON result
    /// with the caller's principal, platform authority and role assignments.</returns>
    private async ValueTask<IResult> DescribeCallerAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal caller,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var (principalId, assignments) = await GetAssignmentsAsync(
            openIdContext,
            caller,
            storeManager,
            cancellationToken
        );

        var platformNode = ResourceNode.For(
            openIdContext.Tenant.TenantId,
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId
        );
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            caller,
            platformNode,
            Operations.Read
        );

        var resource = new CallerResource
        {
            PrincipalId = principalId,
            IsPlatformAdministrator = authorizationResult.Succeeded,
            Assignments = assignments.Select(ToRoleAssignmentResource).ToList(),
        };
        return TypedResults.Json(resource);
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
        return await ListHeldAsync(
            httpContext,
            ResourceNodeTypes.Team,
            async (storeManager, teamIds, ct) =>
            {
                var teams = await storeManager.GetStore<ITeamStore>().GetManyAsync(teamIds, ct);
                return teams.Select(TeamApiEndpointHandler.ToTeamResource).ToList();
            },
            cancellationToken
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
        return await ListHeldAsync(
            httpContext,
            ResourceNodeTypes.Tenant,
            async (storeManager, tenantIds, ct) =>
            {
                var tenants = await storeManager
                    .GetStore<ITenantStore>()
                    .GetManyAsync(tenantIds, ct);
                return tenants
                    .Select(tenant => TenantApiEndpointHandler.ToTenantResource(tenant))
                    .ToList();
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Lists the resources of one node type that the caller holds a role assignment at: reads the caller's role
    /// assignments in the control-plane tenant scope, then reads and maps the resources they name in one batch.
    /// </summary>
    /// <typeparam name="TResource">The type of the resource representation to list.</typeparam>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="nodeType">The <see cref="ResourceNodeTypes"/> value of the nodes to list.</param>
    /// <param name="readAndMapAsync">The delegate that reads the resources with the specified identifiers in one batch
    /// and maps them to their representations.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the JSON result
    /// with the listed resources.</returns>
    private async ValueTask<IResult> ListHeldAsync<TResource>(
        HttpContext httpContext,
        string nodeType,
        Func<
            IStoreManager,
            IReadOnlyCollection<string>,
            CancellationToken,
            ValueTask<List<TResource>>
        > readAndMapAsync,
        CancellationToken cancellationToken
    )
    {
        return await AnswerCallerAsync(
            httpContext,
            async (openIdContext, caller, storeManager, ct) =>
            {
                var (_, assignments) = await GetAssignmentsAsync(
                    openIdContext,
                    caller,
                    storeManager,
                    ct
                );
                var resourceIds = GetResourceIds(assignments, nodeType).ToList();
                var items = await readAndMapAsync(storeManager, resourceIds, ct);
                var collection = new CollectionResource<TResource>
                {
                    Items = items,
                    ContinuationToken = null,
                };
                return TypedResults.Json(collection);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Answers the caller that the management authentication filter authenticated from a management access token,
    /// read from <see cref="IManagementCallerFeature"/>, in the request's control-plane tenant scope through one
    /// store manager; any other request gets the authentication-required problem.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="answerAsync">The delegate that answers the caller from the request's <see cref="OpenIdContext"/>,
    /// the caller's <see cref="ClaimsPrincipal"/> and the <see cref="IStoreManager"/> to read through.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the result that
    /// answers the caller, or the authentication-required problem.</returns>
    private async ValueTask<IResult> AnswerCallerAsync(
        HttpContext httpContext,
        Func<
            OpenIdContext,
            ClaimsPrincipal,
            IStoreManager,
            CancellationToken,
            ValueTask<IResult>
        > answerAsync,
        CancellationToken cancellationToken
    )
    {
        var caller = httpContext.Features.Get<IManagementCallerFeature>()?.Caller;
        if (caller is null)
            return ManagementProblems.AuthenticationRequired();

        var openIdContext = httpContext.GetOpenIdContext();

        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        return await answerAsync(openIdContext, caller, storeManager, cancellationToken);
    }

    /// <summary>
    /// Gets the caller's principal and the role assignments it holds; a caller that resolves to no principal holds none.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> of the caller.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> to read through.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the caller's
    /// principal identifier, or <see langword="null"/> when it resolves to none, and its role assignments.</returns>
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
        var principalId = await PrincipalResolver.ResolvePrincipalIdOrDefaultAsync(
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

    /// <summary>
    /// Gets the distinct identifiers of the nodes of one type that the assignments are held at, ordered ordinally.
    /// </summary>
    /// <param name="assignments">The role assignments to read.</param>
    /// <param name="resourceType">The <see cref="ResourceNodeTypes"/> value of the nodes to select.</param>
    /// <returns>The distinct node identifiers.</returns>
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
