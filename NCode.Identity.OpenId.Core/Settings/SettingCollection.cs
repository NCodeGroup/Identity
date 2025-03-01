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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingCollection"/> abstraction.
/// </summary>
public class SettingCollection : ISettingCollection
{
    private Dictionary<string, Setting> Store { get; }
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingCollection"/> class.
    /// </summary>
    /// <param name="settingDescriptorCollectionProvider">The <see cref="ISettingDescriptorCollectionProvider"/> instance.</param>
    public SettingCollection(ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider)
    {
        Store = new Dictionary<string, Setting>(StringComparer.Ordinal);
        SettingDescriptorCollectionProvider = settingDescriptorCollectionProvider;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingCollection"/> class.
    /// </summary>
    /// <param name="settingDescriptorCollectionProvider">The <see cref="ISettingDescriptorCollectionProvider"/> instance.</param>
    /// <param name="settings">The collection of <see cref="Setting"/> instances.</param>
    public SettingCollection(ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider, IEnumerable<Setting> settings)
    {
        Store = settings.ToDictionary(setting => setting.Descriptor.Name, StringComparer.Ordinal);
        SettingDescriptorCollectionProvider = settingDescriptorCollectionProvider;
    }

    private SettingCollection(ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider, Dictionary<string, Setting> store)
    {
        Store = store;
        SettingDescriptorCollectionProvider = settingDescriptorCollectionProvider;
    }

    /// <inheritdoc />
    public int Count => Store.Count;

    /// <inheritdoc />
    public IEnumerator<Setting> GetEnumerator() => Store.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public TValue GetValue<TValue>(SettingKey<TValue> key)
        where TValue : notnull
    {
        if (TryGet(key, out var setting))
        {
            return setting.Value;
        }

        if (SettingDescriptorCollectionProvider.Collection.TryGet(key, out var descriptor) && descriptor.HasDefault)
        {
            return descriptor.DefaultOrNull;
        }

        throw new KeyNotFoundException();
    }

    /// <inheritdoc />
    public bool TryGet(string settingName, [MaybeNullWhen(false)] out Setting setting)
    {
        return Store.TryGetValue(settingName, out setting);
    }

    /// <inheritdoc />
    public bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out Setting<TValue> setting)
        where TValue : notnull
    {
        if (Store.TryGetValue(key.SettingName, out var baseSetting) && baseSetting is Setting<TValue> typedSetting)
        {
            setting = typedSetting;
            return true;
        }

        setting = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetValue<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out TValue value)
        where TValue : notnull
    {
        if (TryGet(key, out var setting))
        {
            value = setting.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public void Set(Setting setting)
    {
        Store[setting.Descriptor.Name] = setting;
    }

    /// <inheritdoc />
    public void Set<TValue>(SettingKey<TValue> key, TValue value)
        where TValue : notnull
    {
        if (!SettingDescriptorCollectionProvider.Collection.TryGet(key, out var descriptor))
        {
            descriptor = new SettingDescriptor<TValue>
            {
                Name = key.SettingName
            };
        }

        var setting = descriptor.Create(value);

        Store[key.SettingName] = setting;
    }

    /// <inheritdoc />
    public bool Remove<TValue>(SettingKey<TValue> key)
        where TValue : notnull
        => Store.Remove(key.SettingName);

    /// <inheritdoc />
    public ISettingCollection Merge(IEnumerable<Setting> otherCollection)
    {
        var newStore = new Dictionary<string, Setting>(StringComparer.Ordinal);

        // TL;DR: Full Outer Join

        foreach (var otherSetting in otherCollection)
        {
            var baseDescriptor = otherSetting.Descriptor;
            var settingName = baseDescriptor.Name;

            var newSetting = otherSetting;
            if (TryGet(settingName, out var currentSetting))
            {
                newSetting = baseDescriptor.Merge(currentSetting, otherSetting);
            }

            newStore.Add(settingName, newSetting);
        }

        foreach (var currentSetting in Store.Values)
        {
            var settingName = currentSetting.Descriptor.Name;
            newStore.TryAdd(settingName, currentSetting);
        }

        return new SettingCollection(SettingDescriptorCollectionProvider, newStore);
    }
}
