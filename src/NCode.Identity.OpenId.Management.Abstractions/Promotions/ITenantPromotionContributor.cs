// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Contributes one kind of resource to a tenant promotion. The promoter plans every contributor, then applies them one
/// at a time in ascending <see cref="Order"/>, saving after each, so a contributor stages its writes and never saves.
/// </summary>
// See ADR-1425.
[PublicAPI]
public interface ITenantPromotionContributor
{
    /// <summary>
    /// Gets the position of the contributor among the others, ascending. See <see cref="TenantPromotionOrders"/> for
    /// the built-in contributors' positions.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Reads the source and target resources the contributor promotes and records on the context its changes, notes,
    /// errors, identifier mappings and changed limits. Planning writes nothing.
    /// </summary>
    /// <param name="context">The promotion's shared state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask PlanAsync(TenantPromotionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Stages the writes and the audit events the contributor planned. The promoter saves after it returns.
    /// </summary>
    /// <param name="context">The promotion's shared state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask ApplyAsync(TenantPromotionContext context, CancellationToken cancellationToken);
}
