#region Copyright Preamble

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

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a strongly typed collection of <see cref="SettingDescriptor"/> instances that can be accessed by setting name and value type.
/// </summary>
public interface ISettingDescriptorCollection : IReadOnlyCollection<SettingDescriptor>
{
    /// <summary>
    /// Adds the specified <paramref name="descriptor"/> to the collection.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor"/> instance to add.</param>
    void Register(SettingDescriptor descriptor);

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
    bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out SettingDescriptor<TValue> descriptor)
        where TValue : notnull;
}
