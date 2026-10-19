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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Defines a named security profile (for example <c>fapi2</c>) as a single, self-contained unit: the value that
/// selects it, the ceiling settings it injects into the merge lattice, and the behavioral requirements enforced at
/// request time. Consolidating all of a profile's rules in one definition keeps them discoverable and lets a deployment
/// add a profile by registering another implementation, without changing any enforcement site.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are registered as a strategy collection and resolved through <see cref="ISecurityProfileCatalog"/>.
/// </para>
/// <para>
/// A server, tenant, or client selects any number of profiles through the <c>security_profiles</c> setting, and the
/// selections accumulate down the <c>server → tenant → client</c> cascade: a scope can add a profile but never remove
/// one a scope above it selects, and every selected profile applies. Profiles therefore compose by conjunction, and a
/// profile never declares how it ranks against another — selecting several profiles applies all of them, which is at
/// least as strict as each.
/// </para>
/// </remarks>
[PublicAPI]
public interface ISecurityProfile
{
    /// <summary>
    /// Gets the <c>security_profiles</c> value that selects this profile (for example <c>fapi2</c>).
    /// </summary>
    string ProfileName { get; }

    /// <summary>
    /// Gets the behavioral requirements this profile imposes. Each requirement is a self-contained rule object; an
    /// enforcement point resolves the subset assignable to the phase interface it owns and invokes them, so new
    /// requirement kinds are additive. When several selected profiles share a requirement instance, it is evaluated
    /// once.
    /// </summary>
    IReadOnlyCollection<ISecurityProfileRequirement> Requirements { get; }

    /// <summary>
    /// Contributes this profile's ceiling settings through the supplied <paramref name="ceiling"/> builder. The caller
    /// merges the result into the declaring scope's settings so the existing floor/ceiling merge lattice carries the
    /// profile down the <c>server → tenant → client</c> cascade un-loosenably.
    /// </summary>
    /// <remarks>
    /// A ceiling setting merges with a lattice operation — an intersected list, a minimum, a required flag — which the
    /// builder enforces, so the ceilings of several selected profiles bind every scope below the one that selects them
    /// regardless of the order in which they are applied, and no profile can loosen another. The profile chooses the
    /// setting whose bound tightens the server: profiles whose ceilings contradict each other leave nothing permitted
    /// on that dimension, which fails closed. A profile that builds on another selects it here, through the
    /// <c>security_profiles</c> setting, and the catalog then applies that profile's ceilings and requirements too.
    /// </remarks>
    /// <param name="ceiling">The builder that collects this profile's ceiling settings.</param>
    void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling);
}
