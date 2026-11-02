// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Checks a relationship between a value that a client registers and the effective setting that must permit it, or a
/// relationship between a value and the setting that requires it, which the domain of a setting cannot express on its
/// own.
/// </summary>
/// <remarks>
/// Implementations are registered with <c>TryAddEnumerable</c>, so each rule is its own implementation type.
/// </remarks>
// See ADR-1206.
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
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violations of the rule, which is empty when the client settings are coherent.</returns>
    IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context);
}
