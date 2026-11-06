// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Represents a team: the grouping of related tenants whose effective settings are its tenants' parent. A team sits
/// beneath the platform or beneath a parent team.
/// </summary>
// See ADR-1411
// See ADR-1426
[PublicAPI]
public abstract class OpenIdTeam : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for the team.
    /// </summary>
    public abstract string TeamId { get; }

    /// <summary>
    /// Gets the concurrency token of the persisted team this instance was materialized from. Every committed change to
    /// the team issues a new one.
    /// </summary>
    public abstract string ConcurrencyToken { get; }

    /// <summary>
    /// Gets the display name for the team.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether the team or any team above it is disabled. The tenants beneath a disabled team
    /// are not served.
    /// </summary>
    public abstract bool IsDisabled { get; }

    /// <summary>
    /// Gets the materialized team directly above, whose settings this team's are layered over, or <see langword="null"/>
    /// for a top-level team.
    /// </summary>
    public abstract OpenIdTeam? Parent { get; }

    /// <summary>
    /// Gets the team's path in the <c>TeamPaths</c> form, from its top-level team down to itself.
    /// </summary>
    public abstract string Path { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProvider"/> that contains the team's settings merged onto its parent
    /// team's effective settings, or onto the platform's for a top-level team. This collection provider periodically
    /// refreshes the items from the backing store and provides change notifications.
    /// </summary>
    public abstract IReadOnlySettingCollectionProvider SettingsProvider { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    protected abstract ValueTask DisposeAsyncCore();
}
