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

using NCode.Collections.Providers;
using NCode.Collections.Providers.DataSources;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation for the <see cref="IReadOnlySettingCollectionProvider"/> interface.
/// </summary>
public class ReadOnlySettingCollectionProvider(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    IEnumerable<ICollectionDataSource<Setting>> dataSources,
    bool owns = false
) : BaseCollectionProvider<Setting, IReadOnlySettingCollection>(
    new CompositeCollectionDataSource<Setting>(dataSources) { Owns = owns, CombineFunc = Merge }
), IReadOnlySettingCollectionProvider
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } = settingDescriptorCollectionProvider;

    /// <inheritdoc />
    protected override IReadOnlySettingCollection CreateCollection(IEnumerable<Setting> items)
    {
        return new SettingCollection(SettingDescriptorCollectionProvider, items);
    }

    private static IEnumerable<Setting> Merge(IEnumerable<IEnumerable<Setting>> collections)
    {
        var results = new Dictionary<string, Setting>(StringComparer.Ordinal);

        // TL;DR: Full Outer Join

        foreach (var collection in collections)
        {
            foreach (var nextSetting in collection)
            {
                var baseDescriptor = nextSetting.Descriptor;
                var settingName = baseDescriptor.Name;

                if (results.TryGetValue(settingName, out var previousSetting))
                {
                    var mergedSetting = baseDescriptor.Merge(previousSetting, nextSetting);
                    results[settingName] = mergedSetting;
                }
                else
                {
                    results.Add(settingName, nextSetting);
                }
            }
        }

        return results.Values.AsEnumerable();
    }
}
