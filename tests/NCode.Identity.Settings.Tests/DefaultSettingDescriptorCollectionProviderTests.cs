// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Collections.Providers;
using NCode.Collections.Providers.DataSources;

namespace NCode.Identity.Settings;

public class DefaultSettingDescriptorCollectionProviderTests
{
    private static ICollectionProviderFactory CreateFactory() =>
        new ServiceCollection()
            .AddCollectionProviders()
            .BuildServiceProvider()
            .GetRequiredService<ICollectionProviderFactory>();

    private static ICollectionDataSource<SettingDescriptor> DataSource(
        params SettingDescriptor[] descriptors
    ) => new StaticCollectionDataSource<SettingDescriptor>(descriptors);

    #region Collection Tests

    [Fact]
    public async Task Collection_ContainsDescriptorsFromDataSources()
    {
        await using var provider = new DefaultSettingDescriptorCollectionProvider(
            CreateFactory(),
            [
                DataSource(
                    new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] },
                    new SettingDescriptor<int> { Name = "b", AppliesTo = ["client"] }
                ),
            ]
        );

        var collection = provider.Collection;

        Assert.Equal(2, collection.Count);
        Assert.True(collection.TryGet("a", out _));
        Assert.True(collection.TryGet(new SettingKey<int>("b"), out _));
    }

    #endregion

    #region GetChangeToken Tests

    [Fact]
    public async Task GetChangeToken_ReturnsToken()
    {
        await using var provider = new DefaultSettingDescriptorCollectionProvider(
            CreateFactory(),
            [DataSource(new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] })]
        );

        Assert.NotNull(provider.GetChangeToken());
    }

    #endregion
}
