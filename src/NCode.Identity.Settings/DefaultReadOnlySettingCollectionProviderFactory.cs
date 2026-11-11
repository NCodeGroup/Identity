// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="IReadOnlySettingCollectionProviderFactory"/> abstraction.
/// </summary>
internal class DefaultReadOnlySettingCollectionProviderFactory(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : IReadOnlySettingCollectionProviderFactory
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    /// <inheritdoc />
    public IReadOnlySettingCollectionProvider Create(
        IEnumerable<ICollectionDataSource<Setting>> dataSources,
        bool owns = false
    ) =>
        new ReadOnlySettingCollectionProvider(
            SettingDescriptorCollectionProvider,
            dataSources,
            owns
        );
}
