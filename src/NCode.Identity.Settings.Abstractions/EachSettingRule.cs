// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a <see cref="SettingRule{TValue}"/> for a list setting that applies an element rule to every value in the
/// list, and names the first value that does not satisfy it.
/// </summary>
/// <param name="elementRule">The rule that every value in the list must satisfy.</param>
/// <typeparam name="TElement">The type of the values in the list.</typeparam>
internal sealed class EachSettingRule<TElement>(SettingRule<TElement> elementRule)
    : SettingRule<IReadOnlyCollection<TElement>>
    where TElement : notnull
{
    private SettingRule<TElement> ElementRule { get; } = elementRule;

    /// <inheritdoc />
    public override string Description =>
        $"a list of values that are each {ElementRule.Description}";

    /// <inheritdoc />
    public override bool TryValidate(
        IReadOnlyCollection<TElement> value,
        [NotNullWhen(false)] out string? failure
    )
    {
        foreach (var element in value)
        {
            // A JSON array can carry a null that the declared non-nullable element type does not admit.
            if (element is null)
            {
                failure = $"must list only values that are {ElementRule.Description}, not null";
                return false;
            }

            if (!ElementRule.TryValidate(element, out _))
            {
                var text = Convert.ToString(element, CultureInfo.InvariantCulture);
                failure =
                    $"must list only values that are {ElementRule.Description}; '{text}' is not";
                return false;
            }
        }

        failure = null;
        return true;
    }
}
