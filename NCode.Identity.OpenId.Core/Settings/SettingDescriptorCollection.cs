﻿#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorCollection"/> abstraction.
/// </summary>
public class SettingDescriptorCollection(
    IEnumerable<SettingDescriptor> descriptors
) : ISettingDescriptorCollection
{
    private Dictionary<string, SettingDescriptor> Descriptors { get; } =
        descriptors.ToDictionary(x => x.Name, StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryGet(string settingName, [MaybeNullWhen(false)] out SettingDescriptor descriptor) =>
        Descriptors.TryGetValue(settingName, out descriptor);

    /// <inheritdoc />
    public bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out SettingDescriptor<TValue> descriptor)
        where TValue : notnull
    {
        if (!Descriptors.TryGetValue(key.SettingName, out var baseDescriptor) || baseDescriptor is not SettingDescriptor<TValue> typedDescriptor)
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
    public IEnumerator<SettingDescriptor> GetEnumerator() => Descriptors.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
