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
// See ADR-0247.
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
