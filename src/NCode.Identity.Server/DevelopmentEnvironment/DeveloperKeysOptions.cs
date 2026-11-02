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

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Configures how <strong>development-only</strong> signing-key conveniences apply to the OpenID server. Bound from the
/// <c>DeveloperKeys</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class DeveloperKeysOptions
{
    /// <summary>
    /// The configuration section (<c>DeveloperKeys</c>) bound to <see cref="DeveloperKeysOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "DeveloperKeys";

    /// <summary>
    /// Gets or sets a value indicating whether a tenant signing key that can no longer be read is removed. A tenant key
    /// can no longer be read when its master key, or the root key that wraps it, was lost or changed. It is removed when
    /// the tenant is seeded, and provisioning replaces it (ADR-1313). Development-only: Production refuses the section.
    /// </summary>
    public bool PruneUnreadableKeys { get; set; }
}
