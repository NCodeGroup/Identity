// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a <see cref="SettingRule{TValue}"/> that accepts the values satisfying a predicate.
/// </summary>
/// <param name="description">The phrase that describes the accepted values.</param>
/// <param name="predicate">The predicate that a value must satisfy.</param>
/// <typeparam name="TValue">The type of the setting's value.</typeparam>
internal sealed class PredicateSettingRule<TValue>(string description, Func<TValue, bool> predicate)
    : SettingRule<TValue>
    where TValue : notnull
{
    private Func<TValue, bool> Predicate { get; } = predicate;

    /// <inheritdoc />
    public override string Description { get; } = description;

    /// <inheritdoc />
    public override bool TryValidate(TValue value, [NotNullWhen(false)] out string? failure)
    {
        if (Predicate(value))
        {
            failure = null;
            return true;
        }

        failure = $"must be {Description}";
        return false;
    }
}
