// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Holds the <see cref="OpenIdPlatform"/> that this replica materialized, so background work can wait for it and a
/// committed change to its settings can refresh it at once.
/// </summary>
/// <remarks>
/// The registry has no dependencies, so the platform provider can register the platform it materializes without creating
/// a cycle through the services that need the platform.
/// </remarks>
// See ADR-1412.
internal interface IMaterializedPlatformRegistry : IMaterializedPlatformAccessor
{
    /// <summary>
    /// Registers the platform that this replica materialized. A replica materializes its platform once, so a registration
    /// after the first is ignored.
    /// </summary>
    /// <param name="platform">The <see cref="OpenIdPlatform"/> to register.</param>
    void Register(OpenIdPlatform platform);

    /// <summary>
    /// Reads the settings of the registered platform again. Does nothing when no platform is
    /// registered.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RefreshSettingsAsync(CancellationToken cancellationToken);
}
