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
    private Dictionary<SettingKey, Setting> Settings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingCollection"/> class.
    /// </summary>
    public SettingCollection()
    {
        Settings = new Dictionary<SettingKey, Setting>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingCollection"/> class.
    /// </summary>
    /// <param name="settings">The collection of <see cref="Setting"/> instances.</param>
    public SettingCollection(IEnumerable<Setting> settings)
    {
        Settings = settings.ToDictionary(setting => setting.BaseDescriptor.Key);
    }

    private SettingCollection(Dictionary<SettingKey, Setting> settings)
    {
        Settings = settings;
    }

    /// <inheritdoc />
    public bool TryGet(SettingKey key, [MaybeNullWhen(false)] out Setting setting)
        => Settings.TryGetValue(key, out setting);

    /// <inheritdoc />
    public bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out Setting<TValue> setting)
        where TValue : notnull
    {
        if (!Settings.TryGetValue(key, out var baseSetting) || baseSetting is not Setting<TValue> typedSetting)
        {
            setting = default;
            return false;
        }

        setting = typedSetting;
        return true;
    }

    /// <inheritdoc />
    public void Set<TValue>(SettingKey<TValue> key, Setting<TValue> setting)
        where TValue : notnull
        => Settings[key] = setting;

    /// <inheritdoc />
    public bool Remove<TValue>(SettingKey<TValue> key)
        where TValue : notnull
        => Settings.Remove(key);

    /// <inheritdoc />
    public ISettingCollection Merge(ISettingCollection otherCollection)
    {
        var currentCollection = Settings.Values;
        var newCollection = new Dictionary<SettingKey, Setting>();

        // TL;DR: Full Outer Join

        foreach (var currentSetting in currentCollection)
        {
            var baseDescriptor = currentSetting.BaseDescriptor;
            var key = baseDescriptor.Key;

            var newSetting = currentSetting;
            if (otherCollection.TryGet(key, out var otherSetting))
            {
                newSetting = baseDescriptor.Merge(currentSetting, otherSetting);
            }

            newCollection.Add(key, newSetting);
        }

        foreach (var otherSetting in otherCollection)
        {
            var key = otherSetting.BaseDescriptor.Key;
            if (newCollection.ContainsKey(key)) continue;
            newCollection.Add(key, otherSetting);
        }

        return new SettingCollection(newCollection);
    }

    /// <inheritdoc />
    public int Count => Settings.Count;

    /// <inheritdoc />
    public IEnumerator<Setting> GetEnumerator() => Settings.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
