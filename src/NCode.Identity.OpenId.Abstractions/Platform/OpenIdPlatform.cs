// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides contextual information and configuration details for an <c>OAuth</c> or <c>OpenID Connect</c> authorization server.
/// </summary>
[PublicAPI]
public abstract class OpenIdPlatform
{
    /// <summary>
    /// Gets the unique identifier for the server.
    /// </summary>
    public abstract string PlatformId { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProvider"/> which contains settings scoped to the server.
    /// </summary>
    public abstract IReadOnlySettingCollectionProvider SettingsProvider { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProvider"/> which contains the settings the server derives from its
    /// defaults and the host's configuration. <see cref="SettingsProvider"/> merges the server's persisted settings
    /// document onto them, so they are what a replacement document would be merged onto.
    /// </summary>
    public abstract IReadOnlySettingCollectionProvider ConfiguredSettingsProvider { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Reads the server's persisted settings document again now, so a change that any replica committed takes effect in
    /// <see cref="SettingsProvider"/> without waiting for the next periodic refresh.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    public abstract ValueTask RefreshSettingsAsync(CancellationToken cancellationToken);
}
