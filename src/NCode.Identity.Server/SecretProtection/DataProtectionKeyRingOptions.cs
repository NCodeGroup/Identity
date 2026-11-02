// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Configures the ASP.NET Core Data Protection key ring that the server keeps encrypted under its master key. Bound from
/// the <c>DataProtectionKeyRing</c> configuration section.
/// </summary>
/// <remarks>
/// The recovery for a ring whose keys the server can no longer decrypt, because the master key that encrypted them is
/// lost, is to list their IDs in <see cref="RevokedKeyIds"/> and start the host again.
/// </remarks>
// See ADR-1247.
[PublicAPI]
public sealed class DataProtectionKeyRingOptions
{
    /// <summary>
    /// The configuration section (<c>DataProtectionKeyRing</c>) bound to <see cref="DataProtectionKeyRingOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "DataProtectionKeyRing";

    /// <summary>
    /// Gets or sets the IDs of the Data Protection keys that the host revokes as it starts. Empty by default.
    /// </summary>
    /// <remarks>
    /// Revoking is idempotent: a key that is already revoked is skipped, so an entry can stay in the configuration or be
    /// removed once the host has started. A key revoked this way no longer decrypts what it protected, so the cookies,
    /// anti-forgery tokens and other values that the key protected stop validating.
    /// </remarks>
    public Guid[] RevokedKeyIds { get; set; } = [];
}
