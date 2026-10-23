#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Holds the <see cref="OpenIdServer"/> that this replica materialized, so background work can wait for it and a
/// committed change to its settings can refresh it at once.
/// </summary>
/// <remarks>
/// The registry has no dependencies, so the server provider can register the server it materializes without creating
/// a cycle through the services that need the server.
/// </remarks>
// See ADR-0219.
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
