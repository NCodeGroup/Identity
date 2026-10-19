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
