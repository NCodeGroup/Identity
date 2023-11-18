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

using System.Text.Json;

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Provides the ability to get a <see cref="SettingDescriptor"/> for a setting name and <see cref="JsonTokenType"/>.
/// </summary>
/// <remarks>
/// This level of indirection is needed because JSON converters are not mock friendly.
/// </remarks>
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

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorJsonProvider"/> abstraction.
/// </summary>
public class SettingDescriptorJsonProvider : ISettingDescriptorJsonProvider
{
    private ISettingDescriptorCollection SettingDescriptorCollection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingDescriptorJsonProvider"/> class.
    /// </summary>
    /// <param name="settingDescriptorCollection">The <see cref="ISettingDescriptorCollection"/> instance.</param>
    public SettingDescriptorJsonProvider(ISettingDescriptorCollection settingDescriptorCollection)
    {
        SettingDescriptorCollection = settingDescriptorCollection;
    }

    /// <inheritdoc />
    public SettingDescriptor GetDescriptor(string settingName, JsonTokenType jsonTokenType)
    {
        if (SettingDescriptorCollection.TryGet(settingName, out var descriptor))
            return descriptor;

        switch (jsonTokenType)
        {
            case JsonTokenType.String:
                return new SettingDescriptor<string>
                {
                    Name = settingName,
                    OnMerge = KnownSettings.Replace
                };

            case JsonTokenType.True or JsonTokenType.False:
                return new SettingDescriptor<bool>
                {
                    Name = settingName,
                    OnMerge = KnownSettings.Replace
                };

            case JsonTokenType.Number:
                return new SettingDescriptor<double>
                {
                    Name = settingName,
                    OnMerge = KnownSettings.Replace
                };

            case JsonTokenType.StartArray:
                return new SettingDescriptor<IReadOnlyCollection<string>>
                {
                    Name = settingName,
                    OnMerge = KnownSettings.Replace
                };
        }

        return new SettingDescriptor<JsonElement>
        {
            Name = settingName,
            OnMerge = KnownSettings.Replace
        };
    }
}
