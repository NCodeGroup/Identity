// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides extension methods for the <see cref="IReadOnlySettingCollection"/> abstraction.
/// </summary>
[PublicAPI]
public static class SettingCollectionExtensions
{
    /// <param name="collection">The <see cref="IReadOnlySettingCollection"/> instance.</param>
    extension(IReadOnlySettingCollection collection)
    {
        /// <summary>
        /// Gets the value of the value-type setting identified by <paramref name="key"/>, or <c>null</c> when the
        /// setting is not present. Unlike <see cref="IReadOnlySettingCollection.TryGetValue{TValue}"/>, this returns a
        /// genuine <see cref="Nullable{T}"/> so an unset setting is <c>null</c> rather than <c>default(TValue)</c> —
        /// the caller can treat "unset" as "no constraint" without conflating it with a configured zero/false value.
        /// </summary>
        /// <param name="key">The key of the strongly typed value-type setting to get.</param>
        /// <typeparam name="TValue">The value type of the setting.</typeparam>
        /// <returns>The setting's value, or <c>null</c> when the setting is not present.</returns>
        public TValue? GetValueOrNull<TValue>(SettingKey<TValue> key)
            where TValue : struct
        {
            ArgumentNullException.ThrowIfNull(collection);
            return collection.TryGetValue(key, out var value) ? value : null;
        }
    }
}
