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
