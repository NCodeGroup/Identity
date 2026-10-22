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

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Checks a relationship between a value that a client registers and the effective setting that must permit it, which
/// the domain of a setting cannot express on its own.
/// </summary>
/// <remarks>
/// Implementations are registered with <c>TryAddEnumerable</c>, so each rule is its own implementation type.
/// </remarks>
// See ADR-0206.
[PublicAPI]
public interface IClientCoherenceRule
{
    /// <summary>
    /// Gets the names of every setting whose effective value the rule reads.
    /// </summary>
    IReadOnlyCollection<string> SettingNames { get; }

    /// <summary>
    /// Finds the values that a client registers and that an effective setting does not permit.
    /// </summary>
    /// <param name="clientSettings">The effective settings of the client.</param>
    /// <returns>The violations of the rule, which is empty when the client settings are coherent.</returns>
    IReadOnlyList<ClientCoherenceViolation> FindViolations(
        IReadOnlySettingCollection clientSettings
    );
}
