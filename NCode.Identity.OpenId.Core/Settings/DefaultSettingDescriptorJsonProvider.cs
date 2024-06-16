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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorJsonProvider"/> abstraction.
/// </summary>
public class DefaultSettingDescriptorJsonProvider(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : ISettingDescriptorJsonProvider
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } = settingDescriptorCollectionProvider;

    private static SettingDescriptor<TValue> CreateDescriptor<TValue>(string settingName)
        where TValue : notnull => new()
    {
        Name = settingName
    };

    /// <inheritdoc />
    public SettingDescriptor GetDescriptor(string settingName, JsonTokenType jsonTokenType)
    {
        if (SettingDescriptorCollectionProvider.Collection.TryGet(settingName, out var descriptor))
            return descriptor;

        return jsonTokenType switch
        {
            JsonTokenType.String => CreateDescriptor<string>(settingName),
            JsonTokenType.True or JsonTokenType.False => CreateDescriptor<bool>(settingName),
            JsonTokenType.Number => CreateDescriptor<double>(settingName),
            JsonTokenType.StartArray => CreateDescriptor<IReadOnlyCollection<string>>(settingName),
            _ => CreateDescriptor<JsonElement>(settingName)
        };
    }
}
