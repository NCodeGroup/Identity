// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;
using NCode.Collections.Providers.DataSources;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation for the <see cref="IReadOnlySettingCollectionProvider"/> interface.
/// </summary>
internal class ReadOnlySettingCollectionProvider(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    IEnumerable<ICollectionDataSource<Setting>> dataSources,
    bool owns = false
)
    : BaseCollectionProvider<Setting, IReadOnlySettingCollection>(
        new CompositeCollectionDataSource<Setting>(dataSources) { Owns = owns, CombineFunc = Merge }
    ),
        IReadOnlySettingCollectionProvider
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    /// <inheritdoc />
    protected override IReadOnlySettingCollection CreateCollection(IEnumerable<Setting> items)
    {
        return new DefaultSettingCollection(SettingDescriptorCollectionProvider, items);
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
