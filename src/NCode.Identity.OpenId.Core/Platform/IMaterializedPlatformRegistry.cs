// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Holds the <see cref="OpenIdServer"/> that this replica materialized, so background work can wait for it and a
/// committed change to its settings can refresh it at once.
/// </summary>
/// <remarks>
/// The registry has no dependencies, so the server provider can register the server it materializes without creating
/// a cycle through the services that need the server.
/// </remarks>
// See ADR-1219.
internal interface IMaterializedServerRegistry : IMaterializedServerAccessor
{
    /// <summary>
    /// Registers the server that this replica materialized. A replica materializes its server once, so a registration
    /// after the first is ignored.
    /// </summary>
    /// <param name="server">The <see cref="OpenIdServer"/> to register.</param>
    void Register(OpenIdServer server);

    /// <summary>
    /// Reads the settings of the registered server again when its <see cref="OpenIdServer.ServerId"/> equals
    /// <paramref name="serverId"/> ordinally. Does nothing when no server is registered or when the identifiers differ.
    /// </summary>
    /// <param name="serverId">The identifier of the server whose settings changed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RefreshSettingsAsync(string serverId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the settings of the registered server again, whichever server it is. Does nothing when no server is
    /// registered.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RefreshSettingsAsync(CancellationToken cancellationToken);
}
