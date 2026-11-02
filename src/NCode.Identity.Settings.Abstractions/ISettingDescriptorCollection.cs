// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a strongly typed collection of <see cref="SettingDescriptor"/> instances that can be accessed by setting name and value type.
/// </summary>
[PublicAPI]
public interface ISettingDescriptorCollection : IReadOnlyCollection<SettingDescriptor>
{
    /// <summary>
    /// Attempts to get a strongly typed descriptor with the specified setting name.
    /// </summary>
    /// <param name="settingName">The name of the strongly typed descriptor to get.</param>
    /// <param name="descriptor">When this method returns, contains the strongly typed descriptor with the specified name,
    /// it the descriptor is found; otherwise, the default value for the type of the <paramref name="descriptor"/> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the collection contains a descriptor with the specified name; otherwise,
    /// <c>false</c>.</returns>
    bool TryGet(string settingName, [MaybeNullWhen(false)] out SettingDescriptor descriptor);

    /// <summary>
    /// Attempts to get a strongly typed descriptor with the specified setting key.
    /// </summary>
    /// <param name="key">The setting key of the strongly typed descriptor to get.</param>
    /// <param name="descriptor">When this method returns, contains the strongly typed descriptor with the specified setting key,
    /// it the descriptor is found; otherwise, the default value for the type of the <paramref name="descriptor"/> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the collection contains a descriptor with the specified setting key; otherwise,
    /// <c>false</c>.</returns>
    bool TryGet<TValue>(
        SettingKey<TValue> key,
        [MaybeNullWhen(false)] out SettingDescriptor<TValue> descriptor
    )
        where TValue : notnull;
}
