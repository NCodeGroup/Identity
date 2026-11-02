// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Describes the values a setting accepts beyond its type — the setting's domain. A value of the right type that falls
/// outside the domain (a name that matches no registered strategy, an unknown enumerated value, a negative lifetime) is
/// one the server would accept without honoring, so it is rejected wherever a setting enters the system: configuration
/// when the host starts, management writes, and stored documents when they are read.
/// </summary>
/// <remarks>
/// <see cref="SettingRules"/> provides the built-in rules, including <see cref="SettingRules.Must{TValue}"/> for a
/// predicate that consults registered strategies. Derive from this class for a rule they cannot express.
/// </remarks>
/// <typeparam name="TValue">The type of the setting's value.</typeparam>
[PublicAPI]
public abstract class SettingRule<TValue>
    where TValue : notnull
{
    /// <summary>
    /// Gets a phrase that describes the values the rule accepts, written to complete the sentence "The value must be …"
    /// (for example <c>one of 'public' or 'pairwise'</c>).
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Determines whether a value satisfies the rule.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="failure">When this method returns <see langword="false"/>, a phrase that states why the value is
    /// rejected, written to follow the name of the setting (for example <c>must be one of 'public' or 'pairwise'</c>);
    /// otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value satisfies the rule; otherwise, <see langword="false"/>.</returns>
    public abstract bool TryValidate(TValue value, [NotNullWhen(false)] out string? failure);
}
