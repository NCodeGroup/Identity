// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.AspNetCore.Options;

/// <summary>
/// The explicit switch that turns on the relaxations a local inner-loop host needs. Bound from the
/// <c>LocalDevelopment</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// The switch is off when absent, and a host that does not set it runs the strict deployment posture in every host
/// environment. The host environment's name never turns it on.
/// </para>
/// <para>
/// Turning it on permits only these relaxations: skipping the deployment gate and the transport check, sending no
/// <c>Strict-Transport-Security</c> header, redirecting HTTP to HTTPS, serving setup over plain HTTP from a loopback
/// remote address, a wildcard or empty <c>AllowedHosts</c> for the management console, <c>http</c> URLs in BFF and
/// console validation, revoking key-ring keys that cannot be decrypted, no local-root-key warning, and the
/// bootstrap-token OpenAPI example and API-reference secret pre-fill.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class LocalDevelopmentOptions
{
    /// <summary>
    /// The configuration section (<c>LocalDevelopment</c>) bound to <see cref="LocalDevelopmentOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "LocalDevelopment";

    /// <summary>
    /// Gets a value indicating whether the local inner-loop relaxations are on. Defaults to <c>false</c>.
    /// </summary>
    public bool Enabled { get; init; }
}
