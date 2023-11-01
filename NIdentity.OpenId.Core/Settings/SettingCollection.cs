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

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingCollection"/> abstraction.
/// </summary>
public class SettingCollection : ISettingCollection
{
    private Dictionary<string, Setting> Settings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingCollection"/> class.
    /// </summary>
    public SettingCollection()
    {
        Settings = new Dictionary<string, Setting>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingCollection"/> class.
    /// </summary>
    /// <param name="settings">The collection of <see cref="Setting"/> instances.</param>
    public SettingCollection(IEnumerable<Setting> settings)
    {
        Settings = settings.ToDictionary(setting => setting.Descriptor.SettingName);
    }

    private SettingCollection(Dictionary<string, Setting> settings)
    {
        Settings = settings;
    }

    /// <inheritdoc />
    public bool TryGet(string settingName, [MaybeNullWhen(false)] out Setting setting)
        => Settings.TryGetValue(settingName, out setting);

    /// <inheritdoc />
    public bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out Setting<TValue> setting)
        where TValue : notnull
    {
        if (!Settings.TryGetValue(key.SettingName, out var baseSetting) || baseSetting is not Setting<TValue> typedSetting)
        {
            setting = default;
            return false;
        }

        setting = typedSetting;
        return true;
    }

    /// <inheritdoc />
    public void Set(Setting setting)
        => Settings[setting.Descriptor.SettingName] = setting;

    /// <inheritdoc />
    public bool Remove<TValue>(SettingKey<TValue> key)
        where TValue : notnull
        => Settings.Remove(key.SettingName);

    /// <inheritdoc />
    public ISettingCollection Merge(ISettingCollection otherCollection)
    {
        var currentCollection = Settings.Values;
        var newCollection = new Dictionary<string, Setting>(StringComparer.Ordinal);

        // TL;DR: Full Outer Join

        foreach (var currentSetting in currentCollection)
        {
            var baseDescriptor = currentSetting.Descriptor;
            var settingName = baseDescriptor.SettingName;

            var newSetting = currentSetting;
            if (otherCollection.TryGet(settingName, out var otherSetting))
            {
                newSetting = baseDescriptor.Merge(currentSetting, otherSetting);
            }

            newCollection.Add(settingName, newSetting);
        }

        foreach (var otherSetting in otherCollection)
        {
            var settingName = otherSetting.Descriptor.SettingName;
            if (newCollection.ContainsKey(settingName)) continue;
            newCollection.Add(settingName, otherSetting);
        }

        return new SettingCollection(newCollection);
    }

    /// <inheritdoc />
    public int Count => Settings.Count;

    /// <inheritdoc />
    public IEnumerator<Setting> GetEnumerator() => Settings.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
