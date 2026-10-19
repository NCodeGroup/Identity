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

using System.Collections.Frozen;
using System.Globalization;
using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides the built-in <see cref="SettingRule{TValue}"/> instances used by <see cref="SettingDescriptor{TValue}.Rule"/>
/// to declare the values a setting accepts beyond its type.
/// </summary>
/// <remarks>
/// The rules compose: <see cref="Each{TElement}"/> applies a rule to every value of a list setting, and
/// <see cref="EmptyOr"/> admits the empty string that a string setting uses for "unset". A rule whose domain is defined
/// by registered strategies (security profiles, algorithms, client-authentication methods) is a
/// <see cref="Must{TValue}"/> predicate over them, or a <see cref="OneOf"/> built from their names.
/// </remarks>
[PublicAPI]
public static class SettingRules
{
    /// <summary>
    /// Gets a rule that accepts every value of the setting's type. It is the rule of a descriptor that declares none.
    /// </summary>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    public static SettingRule<TValue> Any<TValue>()
        where TValue : notnull => new PredicateSettingRule<TValue>("any value", _ => true);

    /// <summary>
    /// Gets a rule that accepts the values satisfying <paramref name="predicate"/>. The predicate is evaluated each time
    /// a value is validated, so it can consult state that changes, such as a registered strategy collection.
    /// </summary>
    /// <param name="description">The phrase that describes the accepted values, written to complete the sentence
    /// "The value must be …" (for example <c>a registered signing algorithm</c>).</param>
    /// <param name="predicate">The predicate that a value must satisfy.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    public static SettingRule<TValue> Must<TValue>(string description, Func<TValue, bool> predicate)
        where TValue : notnull
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(predicate);

        return new PredicateSettingRule<TValue>(description, predicate);
    }

    /// <summary>
    /// Gets a rule that accepts only the specified <paramref name="values"/>, compared ordinally.
    /// </summary>
    /// <param name="values">The accepted values.</param>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    public static SettingRule<string> OneOf(params IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        List<string> ordered = [.. values.Distinct(StringComparer.Ordinal)];
        var accepted = ordered.ToFrozenSet(StringComparer.Ordinal);
        var description = DescribeOneOf(ordered);

        return new PredicateSettingRule<string>(description, accepted.Contains);
    }

    /// <summary>
    /// Gets a rule that accepts any string except the empty string.
    /// </summary>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    public static SettingRule<string> NonEmpty() =>
        new PredicateSettingRule<string>("a non-empty value", value => value.Length > 0);

    /// <summary>
    /// Gets a rule that accepts the empty string, which a string setting uses for "unset", or a value that satisfies
    /// <paramref name="rule"/>.
    /// </summary>
    /// <param name="rule">The rule that a non-empty value must satisfy.</param>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    public static SettingRule<string> EmptyOr(SettingRule<string> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new PredicateSettingRule<string>(
            $"empty or {rule.Description}",
            value => value.Length == 0 || rule.TryValidate(value, out _)
        );
    }

    /// <summary>
    /// Gets a rule that accepts the values greater than or equal to <paramref name="minimum"/>.
    /// </summary>
    /// <param name="minimum">The smallest accepted value.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    public static SettingRule<TValue> AtLeast<TValue>(TValue minimum)
        where TValue : notnull, IComparable<TValue>
    {
        var description = $"at least {Format(minimum)}";

        return new PredicateSettingRule<TValue>(
            description,
            value => value.CompareTo(minimum) >= 0
        );
    }

    /// <summary>
    /// Gets a rule that accepts the values between <paramref name="minimum"/> and <paramref name="maximum"/>,
    /// inclusive.
    /// </summary>
    /// <param name="minimum">The smallest accepted value.</param>
    /// <param name="maximum">The largest accepted value.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    /// <exception cref="ArgumentException"><paramref name="minimum"/> is greater than <paramref name="maximum"/>.</exception>
    public static SettingRule<TValue> Between<TValue>(TValue minimum, TValue maximum)
        where TValue : notnull, IComparable<TValue>
    {
        if (minimum.CompareTo(maximum) > 0)
        {
            throw new ArgumentException(
                "The minimum must not be greater than the maximum.",
                nameof(minimum)
            );
        }

        var description = $"between {Format(minimum)} and {Format(maximum)}";

        return new PredicateSettingRule<TValue>(
            description,
            value => value.CompareTo(minimum) >= 0 && value.CompareTo(maximum) <= 0
        );
    }

    /// <summary>
    /// Gets a rule for a list setting that accepts a list whose every value satisfies <paramref name="rule"/>; an empty
    /// list is accepted. The rule's failure names the first value that does not satisfy it.
    /// </summary>
    /// <param name="rule">The rule that every value in the list must satisfy.</param>
    /// <typeparam name="TElement">The type of the values in the list.</typeparam>
    /// <returns>The <see cref="SettingRule{TValue}"/> instance.</returns>
    public static SettingRule<IReadOnlyCollection<TElement>> Each<TElement>(
        SettingRule<TElement> rule
    )
        where TElement : notnull
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new EachSettingRule<TElement>(rule);
    }

    private static string Format<TValue>(TValue value)
        where TValue : notnull =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string DescribeOneOf(List<string> values)
    {
        switch (values.Count)
        {
            case 0:
                return "a value from an empty set";
            case 1:
                return $"'{values[0]}'";
            case 2:
                return $"one of '{values[0]}' or '{values[1]}'";
        }

        var leading = values[..^1].Select(value => $"'{value}'");
        var list = string.Join(", ", leading);
        return $"one of {list}, or '{values[^1]}'";
    }
}
