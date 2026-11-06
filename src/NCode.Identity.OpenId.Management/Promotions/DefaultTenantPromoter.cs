// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Teams;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides a default implementation of <see cref="ITenantPromoter"/> that fans a promotion out over the registered
/// <see cref="ITenantPromotionContributor"/> instances in ascending order and saves the shared unit of work after each.
/// </summary>
// See ADR-1425.
internal class DefaultTenantPromoter(
    IEnumerable<ITenantPromotionContributor> contributors,
    IManagementAuditRecorder managementAuditRecorder
) : ITenantPromoter
{
    private IReadOnlyList<ITenantPromotionContributor> Contributors { get; } =
    [.. contributors.OrderBy(x => x.Order)];

    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;

    /// <inheritdoc />
    public async ValueTask PlanAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        foreach (var contributor in Contributors)
        {
            await contributor.PlanAsync(context, cancellationToken);
        }

        ValidateEnvironmentSettings(context);
    }

    /// <inheritdoc />
    public async ValueTask ApplyAsync(
        HttpContext httpContext,
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.Errors.Count > 0)
        {
            throw new InvalidOperationException("A promotion with errors cannot be applied.");
        }

        foreach (var contributor in Contributors)
        {
            var firstAuditEventIndex = context.AuditEvents.Count;

            await contributor.ApplyAsync(context, cancellationToken);
            await context.StoreManager.SaveChangesAsync(cancellationToken);

            for (var i = firstAuditEventIndex; i < context.AuditEvents.Count; ++i)
            {
                await ManagementAuditRecorder.RecordResourceChangeAsync(
                    httpContext,
                    context.AuditEvents[i],
                    cancellationToken
                );
            }
        }
    }

    private static void ValidateEnvironmentSettings(TenantPromotionContext context)
    {
        var seen = new HashSet<(string ResourceType, string ResourceId)>();

        foreach (var entry in context.EnvironmentSettings)
        {
            if (!seen.Add((entry.ResourceType, entry.ResourceId)))
            {
                context.AddError(
                    new TenantPromotionErrorResource
                    {
                        ResourceType = entry.ResourceType,
                        ResourceId = entry.ResourceId,
                        Detail = "The environment settings entry is listed more than once.",
                    }
                );
                continue;
            }

            var isChanged = context.Changes.Any(x =>
                string.Equals(x.ResourceType, entry.ResourceType, StringComparison.Ordinal)
                && string.Equals(x.ResourceId, entry.ResourceId, StringComparison.Ordinal)
            );

            if (!isChanged)
            {
                context.AddError(
                    new TenantPromotionErrorResource
                    {
                        ResourceType = entry.ResourceType,
                        ResourceId = entry.ResourceId,
                        Detail =
                            "The environment settings entry names a resource the promotion does not write.",
                    }
                );
            }
        }
    }
}
