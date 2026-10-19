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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

/// <summary>
/// A dictionary-backed <see cref="ISettingDescriptorCollection"/> for tests that need a real descriptor catalog.
/// </summary>
internal sealed class TestSettingDescriptorCollection(params SettingDescriptor[] descriptors)
    : ISettingDescriptorCollection
{
    private Dictionary<string, SettingDescriptor> Descriptors { get; } =
        descriptors.ToDictionary(descriptor => descriptor.Name, StringComparer.Ordinal);

    public int Count => Descriptors.Count;

    public IEnumerator<SettingDescriptor> GetEnumerator() => Descriptors.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryGet(
        string settingName,
        [MaybeNullWhen(false)] out SettingDescriptor descriptor
    ) => Descriptors.TryGetValue(settingName, out descriptor);

    public bool TryGet<TValue>(
        SettingKey<TValue> key,
        [MaybeNullWhen(false)] out SettingDescriptor<TValue> descriptor
    )
        where TValue : notnull
    {
        if (
            Descriptors.TryGetValue(key.SettingName, out var baseDescriptor)
            && baseDescriptor is SettingDescriptor<TValue> typedDescriptor
        )
        {
            descriptor = typedDescriptor;
            return true;
        }

        descriptor = null;
        return false;
    }
}
