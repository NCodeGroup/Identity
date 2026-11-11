// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Promotions;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Registration.AspNetCore;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/*

POST api/teams/{teamId}/promotions/preview
POST api/teams/{teamId}/promotions

*/

/// <summary>
/// Provides the API endpoints for promoting one tenant's configuration into another tenant of the same team: a
/// preview that reports what the promotion changes and writes nothing, and the promotion that applies it.
/// </summary>
// See ADR-1425.
internal class TenantPromotionApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICryptoService cryptoService,
    IAmbientTenantAccessor ambientTenantAccessor,
    ITenantPromoter tenantPromoter,
    IManagementAuditRecorder managementAuditRecorder
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private ITenantPromoter TenantPromoter { get; } = tenantPromoter;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var promotions = endpoints
            .MapGroup("/teams/{teamId}/promotions")
            .WithTags(OpenIdConstants.EndpointTags.Teams);

        promotions
            .MapPost("/preview", PreviewPromotionAsync)
            .Produces<TenantPromotionResource>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        promotions
            .MapPost("", PromoteAsync)
            .Produces<TenantPromotionResource>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
    }

    /// <summary>
    /// Previews the promotion of one tenant's configuration into another tenant of the team: what it would change, what
    /// it notes and what refuses it. A preview writes nothing.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team both tenants belong to.</param>
    /// <param name="request">The tenants and the environment-bound settings the target keeps.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The report of the promotion, which is not applied.</response>
    /// <response code="400">The request names no distinct source and target tenant.</response>
    /// <response code="403">The caller lacks the authority the promotion needs.</response>
    /// <response code="404">A tenant does not exist or is not the team's.</response>
    [EndpointName("api/teams/promotions/preview")]
    internal virtual async ValueTask<IResult> PreviewPromotionAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] PromoteTenantRequest request,
        CancellationToken cancellationToken
    )
    {
        return await PlanPromotionAsync(
            httpContext,
            teamId,
            request,
            (context, _) =>
                ValueTask.FromResult<IResult>(
                    TypedResults.Ok(ToReport(teamId, context, isApplied: false))
                ),
            cancellationToken
        );
    }

    /// <summary>
    /// Promotes one tenant's configuration into another tenant of the team. A promotion that has errors writes nothing.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team both tenants belong to.</param>
    /// <param name="request">The tenants and the environment-bound settings the target keeps.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The report of the promotion, which is applied.</response>
    /// <response code="400">The request names no distinct source and target tenant.</response>
    /// <response code="403">The caller lacks the authority the promotion needs.</response>
    /// <response code="404">A tenant does not exist or is not the team's.</response>
    /// <response code="409">The promotion has errors, so nothing was written.</response>
    /// <response code="412">Another change wrote the target first.</response>
    [EndpointName("api/teams/promotions/create")]
    internal virtual async ValueTask<IResult> PromoteAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] PromoteTenantRequest request,
        CancellationToken cancellationToken
    )
    {
        return await PlanPromotionAsync(
            httpContext,
            teamId,
            request,
            (context, token) => ApplyPromotionAsync(httpContext, teamId, context, token),
            cancellationToken
        );
    }

    // Applies a planned promotion that no authority check refused, unless the plan has errors, then records the audit
    // event and announces the target tenant.
    private async ValueTask<IResult> ApplyPromotionAsync(
        HttpContext httpContext,
        string teamId,
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.Errors.Count > 0)
        {
            return TypedResults.Problem(
                detail: "The promotion cannot be applied; preview it to see why.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["errors"] = context.Errors }
            );
        }

        await TenantPromoter.ApplyAsync(httpContext, context, cancellationToken);

        var report = ToReport(teamId, context, isApplied: true);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            teamId,
            context.TargetTenant.TenantId,
            ResourceChangeTypes.Promoted,
            ToResourceValues(report)
        );

        await NotifyTenantChangedAsync(
            httpContext,
            context.TargetTenant.TenantId,
            settingsChanged: true,
            cancellationToken
        );

        return TypedResults.Ok(report);
    }

    // Refuses a request that names no distinct pair of tenants and a caller without authority at the tenants' nodes. It
    // reads nothing, so a caller without authority learns nothing about either tenant (ADR-1424).
    private async ValueTask<IResult?> GetRefusalBeforeReadingAsync(
        HttpContext httpContext,
        PromoteTenantRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.SourceTenantId)
            || string.IsNullOrWhiteSpace(request.TargetTenantId)
            || string.Equals(
                request.SourceTenantId,
                request.TargetTenantId,
                StringComparison.Ordinal
            )
        )
        {
            return TypedResults.Problem(
                detail: "The promotion needs a source tenant and a different target tenant.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var sourceFailure = await GetAuthorizationFailureAsync(
            httpContext,
            GetTenantNode(request.SourceTenantId),
            Operations.Read,
            cancellationToken
        );
        if (sourceFailure is not null)
        {
            return sourceFailure;
        }

        return await GetAuthorizationFailureAsync(
            httpContext,
            GetTenantNode(request.TargetTenantId),
            Operations.Create,
            cancellationToken
        );
    }

    // Refuses before reading, reads both tenants, plans the promotion, then refuses it when the caller lacks the
    // authority it needs: Update at the node above each changed limit (ADR-1418), then Create at each node whose
    // role-holding client it changes (ADR-1419). Only a promotion that no check refused reaches completeAsync, while
    // the unit of work is still open.
    private async ValueTask<IResult> PlanPromotionAsync(
        HttpContext httpContext,
        string teamId,
        PromoteTenantRequest request,
        Func<TenantPromotionContext, CancellationToken, ValueTask<IResult>> completeAsync,
        CancellationToken cancellationToken
    )
    {
        var refusalBeforeReading = await GetRefusalBeforeReadingAsync(
            httpContext,
            request,
            cancellationToken
        );
        if (refusalBeforeReading is not null)
        {
            return refusalBeforeReading;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var source = await tenantStore.GetOrDefaultAsync(request.SourceTenantId, cancellationToken);
        var target = await tenantStore.GetOrDefaultAsync(request.TargetTenantId, cancellationToken);
        if (
            source is null
            || target is null
            || !string.Equals(source.TeamId, teamId, StringComparison.Ordinal)
            || !string.Equals(target.TeamId, teamId, StringComparison.Ordinal)
        )
        {
            return ManagementProblems.ResourceNotFound();
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var context = new TenantPromotionContext
        {
            SourceTenant = source,
            TargetTenant = target,
            TeamNode = GetTeamNode(httpContext, teamId),
            TargetTenantNode = GetTenantNode(target.TenantId),
            StoreManager = storeManager,
            AmbientTenantAccessor = AmbientTenantAccessor,
            JsonSerializerOptions = openIdContext.Environment.JsonSerializerOptions,
            EnvironmentSettings = request.EnvironmentSettings,
            TargetTenantSettings = target.Settings.Value,
        };

        await TenantPromoter.PlanAsync(context, cancellationToken);

        var limitRefusal = await GetPromotedLimitRefusalOrDefaultAsync(
            httpContext,
            context,
            cancellationToken
        );
        if (limitRefusal is not null)
        {
            return limitRefusal;
        }

        var custodyRefusal = await GetCustodyRefusalOrDefaultAsync(
            httpContext,
            context,
            cancellationToken
        );
        if (custodyRefusal is not null)
        {
            return custodyRefusal;
        }

        return await completeAsync(context, cancellationToken);
    }

    // Groups the changed limits by the node above their document, in the order they were recorded, and returns the first
    // refusal of a group the caller may not update.
    private async ValueTask<IResult?> GetPromotedLimitRefusalOrDefaultAsync(
        HttpContext httpContext,
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var groups = context.ChangedLimits.GroupBy<TenantPromotionLimitChange, ISupportTenantId>(
            x => x.AuthorityAbove,
            ReferenceEqualityComparer.Instance
        );

        foreach (var group in groups)
        {
            var names = group
                .SelectMany(x => x.SettingNames)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            var limitRefusal = await GetLimitChangeRefusalOrDefaultAsync(
                httpContext,
                names,
                group.Key,
                cancellationToken
            );
            if (limitRefusal is not null)
            {
                return limitRefusal;
            }
        }

        return null;
    }

    // Changing a target client whose principal holds role assignments needs the authority to grant those roles, so the
    // caller needs Create at each node the roles are held at.
    private async ValueTask<IResult?> GetCustodyRefusalOrDefaultAsync(
        HttpContext httpContext,
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var checkedNodes = new List<(string TenantId, string ResourceType, string ResourceId)>();

        foreach (var node in context.RequiredCreateAuthorities)
        {
            var key = node is IResourceNode resourceNode
                ? (resourceNode.TenantId, resourceNode.ResourceType, resourceNode.ResourceId)
                : (node.TenantId, string.Empty, string.Empty);

            var isChecked = checkedNodes.Exists(x =>
                string.Equals(x.TenantId, key.Item1, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ResourceType, key.Item2, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ResourceId, key.Item3, StringComparison.OrdinalIgnoreCase)
            );
            if (isChecked)
            {
                continue;
            }

            checkedNodes.Add(key);

            var failure = await GetAuthorizationFailureAsync(
                httpContext,
                node,
                Operations.Create,
                cancellationToken
            );
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static TenantPromotionResource ToReport(
        string teamId,
        TenantPromotionContext context,
        bool isApplied
    ) =>
        new()
        {
            TeamId = teamId,
            SourceTenantId = context.SourceTenant.TenantId,
            TargetTenantId = context.TargetTenant.TenantId,
            IsApplied = isApplied,
            Changes = [.. context.Changes],
            Notes = [.. context.Notes],
            Errors = [.. context.Errors],
        };
}
