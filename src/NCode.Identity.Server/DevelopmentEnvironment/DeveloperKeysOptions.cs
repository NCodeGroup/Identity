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
/// Configures how <strong>development-only</strong> signing keys are provided to the OpenID server. Bound from the
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
    /// Gets or sets the <see cref="DeveloperKeyMode"/> that selects how signing keys are provided, or <c>null</c> when
    /// unset, which selects <see cref="DeveloperKeyMode.Ephemeral"/>. A Production deployment must state
    /// <see cref="DeveloperKeyMode.None"/> explicitly.
    /// </summary>
    public DeveloperKeyMode? Mode { get; set; }
}
