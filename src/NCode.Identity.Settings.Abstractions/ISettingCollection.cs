// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a strongly typed collection of <see cref="Setting"/> instances that can be accessed by name and value type.
/// </summary>
[PublicAPI]
public interface ISettingCollection : IReadOnlySettingCollection
{
    /// <summary>
    /// Add or updates a strongly typed setting in the collection.
    /// </summary>
    /// <param name="setting">The strongly typed setting to add or update.</param>
    void Set(Setting setting);

    /// <summary>
    /// Add or updates a strongly typed setting in the collection, using the setting's registered descriptor.
    /// </summary>
    /// <param name="key">The key of the strongly typed setting to add or update.</param>
    /// <param name="value">The value of the strongly typed setting to set.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <exception cref="InvalidOperationException">The setting has no registered descriptor, or its registered
    /// descriptor's value type is not <typeparamref name="TValue"/>.</exception>
    void Set<TValue>(SettingKey<TValue> key, TValue value)
        where TValue : notnull;

    /// <summary>
    /// Removes a strongly typed setting with the specified key.
    /// </summary>
    /// <param name="key">The key of the strongly typed setting to remove.</param>
    /// <typeparam name="TValue">The type of the setting's value.</typeparam>
    /// <returns><c>true</c> if the setting is successfully found and removed; otherwise, <c>false</c>.
    /// The method returns <c>false</c> if <paramref name="key"/> is not found in the collection.</returns>
    bool Remove<TValue>(SettingKey<TValue> key)
        where TValue : notnull;
}
