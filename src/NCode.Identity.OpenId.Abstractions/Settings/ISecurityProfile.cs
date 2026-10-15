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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Defines a named security profile (for example <c>fapi2</c>) as a single, self-contained unit: the value that
/// activates it, the ceiling settings it injects into the merge lattice, and the behavioral requirements enforced at
/// request time. Consolidating all of a profile's rules in one definition keeps them discoverable and lets a deployment
/// add a profile by registering another implementation, without changing any enforcement site.
/// </summary>
/// <remarks>
/// Implementations are registered as a strategy collection and resolved through <see cref="ISecurityProfileCatalog"/>.
/// </remarks>
[PublicAPI]
public interface ISecurityProfile
{
    /// <summary>
    /// Gets the <c>security_profile</c> setting value that activates this profile (for example <c>fapi2</c>).
    /// </summary>
    string ProfileName { get; }

    /// <summary>
    /// Gets the behavioral requirements this profile imposes. Each requirement is a self-contained rule object; an
    /// enforcement point resolves the subset assignable to the phase interface it owns and invokes them, so new
    /// requirement kinds are additive.
    /// </summary>
    IReadOnlyCollection<ISecurityProfileRequirement> Requirements { get; }

    /// <summary>
    /// Contributes this profile's ceiling settings into the supplied <paramref name="ceiling"/> collection. The caller
    /// merges the result into the declaring scope's settings so the existing floor/ceiling merge lattice carries the
    /// profile down the <c>server → tenant → client</c> cascade un-loosenably.
    /// </summary>
    /// <param name="ceiling">The (initially empty) collection to populate with this profile's ceiling settings.</param>
    void ApplyCeilingSettings(ISettingCollection ceiling);
}
