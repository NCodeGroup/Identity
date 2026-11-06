// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Contains what a promotion does for one client of the source tenant that the host does not manage: the target it
/// reaches, the resource server it acts for, the settings document it writes, and the errors that refuse it.
/// </summary>
// See ADR-1425.
internal sealed class ClientPromotion
{
    /// <summary>
    /// Gets the client of the source tenant.
    /// </summary>
    public required PersistedClient Source { get; init; }

    /// <summary>
    /// Gets the client of the target tenant that has the source's client identifier, or <c>null</c> when the
    /// promotion creates it.
    /// </summary>
    public required PersistedClient? Target { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client acts for in the target tenant, or <c>null</c> for an
    /// ordinary client.
    /// </summary>
    public required string? TargetResourceServerId { get; init; }

    /// <summary>
    /// Gets the settings document the client holds once promoted.
    /// </summary>
    public required PromotedSettings Settings { get; init; }

    /// <summary>
    /// Gets the action the promotion takes, one of the <c>TenantPromotionActions</c> values.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets the reasons the promotion refuses the client.
    /// </summary>
    public required List<string> Errors { get; init; }

    /// <summary>
    /// Gets a value indicating whether the settings document the target holds differs from the promoted document.
    /// </summary>
    public bool IsSettingsChanged { get; init; }

    /// <summary>
    /// Gets a value indicating whether the promoted client needs a credential the target does not hold.
    /// </summary>
    public bool IsCredentialRequired { get; init; }
}
