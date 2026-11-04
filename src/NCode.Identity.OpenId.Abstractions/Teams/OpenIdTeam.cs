// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Represents a team: the grouping of related tenants beneath the platform, whose effective settings are its tenants'
/// parent.
/// </summary>
// See ADR-1403
[PublicAPI]
public abstract class OpenIdTeam : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for the team.
    /// </summary>
    public abstract string TeamId { get; }

    /// <summary>
    /// Gets the display name for the team.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether the team is disabled.
    /// </summary>
    public abstract bool IsDisabled { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProvider"/> that contains team settings merged with platform settings.
    /// This collection provider will periodically refresh the items from the backing store and provide change notifications.
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
