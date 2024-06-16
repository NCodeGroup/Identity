#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the ability to get a <see cref="SettingDescriptor"/> for a setting name and <see cref="JsonTokenType"/>.
/// </summary>
/// <remarks>
/// This level of indirection is needed because JSON converters are not mock friendly.
/// </remarks>
[PublicAPI]
public interface ISettingDescriptorJsonProvider
{
    /// <summary>
    /// Gets a strongly typed <see cref="SettingDescriptor"/> with the specified setting name and <see cref="JsonTokenType"/>.
    /// </summary>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="jsonTokenType">The <see cref="JsonTokenType"/> value.</param>
    /// <returns>The <see cref="SettingDescriptor"/> instance.</returns>
    SettingDescriptor GetDescriptor(string settingName, JsonTokenType jsonTokenType);
}
