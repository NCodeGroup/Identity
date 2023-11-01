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
public interface IJsonSettingDescriptorProvider
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
/// Provides a default implementation of the <see cref="IJsonSettingDescriptorProvider"/> abstraction.
/// </summary>
public class JsonSettingDescriptorProvider : IJsonSettingDescriptorProvider
{
    private ISettingDescriptorProvider SettingDescriptorProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSettingDescriptorProvider"/> class.
    /// </summary>
    /// <param name="settingDescriptorProvider">The <see cref="ISettingDescriptorProvider"/> instance.</param>
    public JsonSettingDescriptorProvider(ISettingDescriptorProvider settingDescriptorProvider)
    {
        SettingDescriptorProvider = settingDescriptorProvider;
    }

    /// <inheritdoc />
    public SettingDescriptor GetDescriptor(string settingName, JsonTokenType jsonTokenType)
    {
        if (SettingDescriptorProvider.TryGet(settingName, out var descriptor))
            return descriptor;

        static bool MergeAnd(bool current, bool other) => current && other;
        static TValue MergeOther<TValue>(TValue current, TValue other) => other;
        static List<TItem> MergeIntersect<TItem>(IEnumerable<TItem> current, IEnumerable<TItem> other) => current.Intersect(other).ToList();

        switch (jsonTokenType)
        {
            case JsonTokenType.String:
                return new SettingDescriptor<string>
                {
                    SettingName = settingName,
                    OnMerge = MergeOther
                };

            case JsonTokenType.True or JsonTokenType.False:
                return new SettingDescriptor<bool>
                {
                    SettingName = settingName,
                    OnMerge = MergeAnd
                };

            case JsonTokenType.Number:
                return new SettingDescriptor<double>
                {
                    SettingName = settingName,
                    OnMerge = MergeOther
                };

            case JsonTokenType.StartArray:
                return new SettingDescriptor<List<string>>
                {
                    SettingName = settingName,
                    OnMerge = MergeIntersect
                };
        }

        return new SettingDescriptor<JsonElement>
        {
            SettingName = settingName,
            OnMerge = MergeOther
        };
    }
}
