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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// The registry of the <see cref="ITenantEnvironmentType"/> implementations known to the OP. It resolves the
/// environment type a tenant's settings select and turns that type into the <c>security_profiles</c> the tenant gets.
/// </summary>
// See ADR-0287.
[PublicAPI]
public interface ITenantEnvironmentCatalog
{
    /// <summary>
    /// Attempts to get the registered type named <paramref name="typeName"/>.
    /// </summary>
    /// <param name="typeName">The <c>environment_type</c> value to look up (for example <c>production</c>).</param>
    /// <param name="type">When this method returns, contains the matching type, if one is registered.</param>
    /// <returns><c>true</c> if a type is registered for the name; otherwise, <c>false</c>.</returns>
    bool TryGetType(string typeName, [MaybeNullWhen(false)] out ITenantEnvironmentType type);

    /// <summary>
    /// Gets the type that the supplied <paramref name="settings"/> select through their effective
    /// <c>environment_type</c> value.
    /// </summary>
    /// <param name="settings">The effective settings of the scope to evaluate.</param>
    /// <returns>The registered type the settings select.</returns>
    /// <exception cref="InvalidOperationException">The settings select a type that is not registered; the scope is
    /// never treated as if it had selected another type.</exception>
    ITenantEnvironmentType GetEffectiveType(IReadOnlySettingCollection settings);

    /// <summary>
    /// Returns the supplied <paramref name="scopeSettings"/> with the <c>security_profiles</c> that the scope's
    /// environment type implies, so the existing security-profile expansion applies their ceilings and defaults.
    /// </summary>
    /// <remarks>
    /// When the scope's own settings document leaves <c>environment_type</c> unset, the type is read from
    /// <paramref name="parentSettings"/>, the parent scope's effective settings, whose descriptor default is
    /// <c>production</c>. At a scope that <c>environment_type</c> does not apply to, the scope settings are returned
    /// unchanged. Otherwise the <c>security_profiles</c> of the resolved type are unioned (ordinal) into the scope's
    /// <c>security_profiles</c> setting. The guardrails therefore follow the tenant's effective type, not the server's.
    /// </remarks>
    /// <param name="parentSettings">The effective settings of the parent scope.</param>
    /// <param name="scopeName">The name of the registered scope whose settings <paramref name="scopeSettings"/> are.</param>
    /// <param name="scopeSettings">The scope's own (not-yet-cascaded) settings.</param>
    /// <returns>The scope settings with the implied profiles selected.</returns>
    /// <exception cref="InvalidOperationException">The resolved environment type is not registered.</exception>
    IReadOnlyCollection<Setting> SelectImpliedProfiles(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        IReadOnlyCollection<Setting> scopeSettings
    );
}
