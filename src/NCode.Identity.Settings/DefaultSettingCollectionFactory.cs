// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingCollectionFactory"/> abstraction.
/// </summary>
internal class DefaultSettingCollectionFactory(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : ISettingCollectionFactory
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    /// <inheritdoc />
    public ISettingCollection Create()
    {
        return new DefaultSettingCollection(SettingDescriptorCollectionProvider);
    }

    /// <inheritdoc />
    public ISettingCollection Create(IEnumerable<Setting> settings)
    {
        return new DefaultSettingCollection(SettingDescriptorCollectionProvider, settings);
    }
}
