// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorCollection"/> abstraction.
/// </summary>
internal class DefaultSettingDescriptorCollection(IEnumerable<SettingDescriptor> descriptors)
    : ISettingDescriptorCollection
{
    private FrozenDictionary<string, SettingDescriptor> Descriptors { get; } =
        descriptors.ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryGet(
        string settingName,
        [MaybeNullWhen(false)] out SettingDescriptor descriptor
    ) => Descriptors.TryGetValue(settingName, out descriptor);

    /// <inheritdoc />
    public bool TryGet<TValue>(
        SettingKey<TValue> key,
        [MaybeNullWhen(false)] out SettingDescriptor<TValue> descriptor
    )
        where TValue : notnull
    {
        if (
            !Descriptors.TryGetValue(key.SettingName, out var baseDescriptor)
            || baseDescriptor is not SettingDescriptor<TValue> typedDescriptor
        )
        {
            descriptor = null;
            return false;
        }

        descriptor = typedDescriptor;
        return true;
    }

    /// <inheritdoc />
    public int Count => Descriptors.Count;

    /// <inheritdoc />
    public IEnumerator<SettingDescriptor> GetEnumerator() =>
        ((IEnumerable<SettingDescriptor>)Descriptors.Values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
