// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Platform;

/// <summary>
/// Represents the REST resource for an OpenID Server's <b>effective</b> (resolved) settings: the server's own settings
/// with descriptor defaults applied. The server is the root of the settings merge hierarchy, so this is the baseline
/// that tenants (and in turn clients) inherit from, including settings that are not advertised in discovery.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PlatformEffectiveSettingsResource
{
    /// <summary>
    /// Gets the platform's effective settings as a flat JSON object of setting name to formatted value, using the same
    /// value representation as the discovery document.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
