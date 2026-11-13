// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Defines a service that refuses a client's <c>jwks_uri</c> when the server can never fetch it, so every entry point
/// that stores a client's settings applies the same check.
/// </summary>
// See ADR-1106.
internal interface IClientJwksUriChecker
{
    /// <summary>
    /// Refuses a <c>jwks_uri</c> whose host resolves only to addresses the server's outbound network guard refuses,
    /// because the server can never fetch such a key set. A value that is absent, unchanged from the stored settings,
    /// or not an absolute <c>https</c> URI is not checked here.
    /// </summary>
    /// <param name="settings">The client's settings document to verify.</param>
    /// <param name="storedSettings">The client's stored settings document, or <see langword="null"/> when the client
    /// is being created.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> that names the <c>jwks_uri</c> setting, or <see langword="null"/> when
    /// the value is not refused.</returns>
    ValueTask<SettingError?> GetRefusalAsync(
        JsonElement settings,
        JsonElement? storedSettings,
        CancellationToken cancellationToken
    );
}
