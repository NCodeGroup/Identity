// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Contains what a promotion does for one non-system resource server of the source tenant: the target it reaches, the
/// settings document it writes, the scopes it adds or changes, and the errors that refuse it.
/// </summary>
// See ADR-1425.
internal sealed class ResourceServerPromotion
{
    /// <summary>
    /// Gets the resource server of the source tenant.
    /// </summary>
    public required PersistedResourceServer Source { get; init; }

    /// <summary>
    /// Gets the resource server of the target tenant that has the source's audience identifier, or <c>null</c> when
    /// the promotion creates it.
    /// </summary>
    public required PersistedResourceServer? Target { get; init; }

    /// <summary>
    /// Gets the opaque identifier the resource server has in the target tenant.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// Gets the settings document the resource server holds once promoted.
    /// </summary>
    public required PromotedSettings Settings { get; init; }

    /// <summary>
    /// Gets the non-system scopes of the source that the target lacks.
    /// </summary>
    public required List<PersistedScope> MissingScopes { get; init; }

    /// <summary>
    /// Gets the non-system scopes of the source whose description differs from the target's scope of that value.
    /// </summary>
    public required List<PersistedScope> ChangedScopes { get; init; }

    /// <summary>
    /// Gets the action the promotion takes, one of the <c>TenantPromotionActions</c> values.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets the reasons the promotion refuses the resource server.
    /// </summary>
    public required List<string> Errors { get; init; }

    /// <summary>
    /// Gets a value indicating whether the settings document the target holds differs from the promoted document.
    /// </summary>
    public bool IsSettingsChanged { get; init; }
}
