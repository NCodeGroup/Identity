// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Coordinates the <see cref="ITenantPromotionContributor"/> instances that plan and apply one tenant promotion.
/// </summary>
// See ADR-1425.
internal interface ITenantPromoter
{
    /// <summary>
    /// Plans the promotion by asking every contributor, in ascending order, to record its changes, notes and errors on
    /// the context, then checks the supplied environment-bound settings against the recorded changes. Planning writes
    /// nothing.
    /// </summary>
    /// <param name="context">The promotion's shared state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask PlanAsync(TenantPromotionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a planned promotion by asking every contributor, in ascending order, to stage its writes, saving the unit
    /// of work after each, and recording the audit events the contributor added.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="context">The promotion's shared state, planned without errors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">The planned promotion has errors.</exception>
    ValueTask ApplyAsync(
        HttpContext httpContext,
        TenantPromotionContext context,
        CancellationToken cancellationToken
    );
}
