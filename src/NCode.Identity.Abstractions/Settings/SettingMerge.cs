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

namespace NCode.Identity.Settings;

/// <summary>
/// Provides the built-in functions used by <see cref="SettingDescriptor{TValue}.OnMerge"/> to merge a parent scope's
/// setting value with a child scope's value (the merge is always <c>merge(parent, child)</c>).
/// </summary>
/// <remarks>
/// The functions come in dual pairs — a <em>meet</em> (narrowing) and a <em>join</em> (widening), plus the two
/// projections. Whether a given function expresses a <em>ceiling</em> (the parent caps the child) or a <em>floor</em>
/// (the parent sets an un-loosenable minimum) depends on the setting's value polarity, so the descriptor author selects
/// the function whose polarity realizes the intended policy:
/// <list type="bullet">
/// <item><description>Ordered values: <see cref="Min{T}"/> / <see cref="Max{T}"/>.</description></item>
/// <item><description>Booleans: <see cref="And"/> / <see cref="Or"/>.</description></item>
/// <item><description>Collections: <see cref="Intersect{T}"/> / <see cref="Union{T}"/>.</description></item>
/// <item><description>Projections: <see cref="Keep{T}"/> (parent wins) / <see cref="Replace{T}"/> (child wins).</description></item>
/// </list>
/// </remarks>
[PublicAPI]
public static class SettingMerge
{
    /// <summary>
    /// A projection that keeps the parent (current) value, ignoring the child; the value is immutable downward.
    /// </summary>
    public static T Keep<T>(T current, T _) => current;

    /// <summary>
    /// A projection that replaces the parent value with the child (other) value; the child freely overrides.
    /// </summary>
    public static T Replace<T>(T _, T other) => other;

    /// <summary>
    /// The meet on a totally ordered type; returns the lesser of the two values.
    /// </summary>
    public static T Min<T>(T current, T other)
        where T : IComparable<T> => current.CompareTo(other) <= 0 ? current : other;

    /// <summary>
    /// The join on a totally ordered type; returns the greater of the two values.
    /// </summary>
    public static T Max<T>(T current, T other)
        where T : IComparable<T> => current.CompareTo(other) >= 0 ? current : other;

    /// <summary>
    /// The boolean meet; returns <see langword="true"/> only when both values are <see langword="true"/>.
    /// </summary>
    public static bool And(bool current, bool other) => current && other;

    /// <summary>
    /// The boolean join; returns <see langword="true"/> when either value is <see langword="true"/>.
    /// </summary>
    public static bool Or(bool current, bool other) => current || other;

    /// <summary>
    /// The set meet; returns the values present in both collections.
    /// </summary>
    public static List<T> Intersect<T>(IEnumerable<T> current, IEnumerable<T> other) =>
        current.Intersect(other).ToList();

    /// <summary>
    /// The set join; returns the de-duplicated values present in either collection.
    /// </summary>
    public static List<T> Union<T>(IEnumerable<T> current, IEnumerable<T> other) =>
        current.Union(other).ToList();
}
