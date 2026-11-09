// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;
using NCode.Collections.Providers.DataSources;

namespace NCode.Identity.Settings;

public class ReadOnlySettingCollectionProviderTests : BaseTests
{
    private Mock<ISettingDescriptorCollectionProvider> MockDescriptorProvider { get; }

    public ReadOnlySettingCollectionProviderTests()
    {
        MockDescriptorProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
    }

    private static Setting<string> Setting(string name, string value) =>
        new SettingDescriptor<string> { Name = name, AppliesTo = ["client"] }.Create(value);

    private static ICollectionDataSource<Setting> DataSource(params Setting[] settings) =>
        new StaticCollectionDataSource<Setting>(settings);

    #region Collection Tests

    [Fact]
    public async Task Collection_MergesDataSources_LastValueWins()
    {
        await using var provider = new ReadOnlySettingCollectionProvider(
            MockDescriptorProvider.Object,
            [
                DataSource(Setting("a", "1"), Setting("b", "2")),
                DataSource(Setting("a", "override"), Setting("c", "3")),
            ]
        );

        var collection = provider.Collection;

        Assert.Equal(3, collection.Count);
        Assert.Equal("override", collection.GetValue(new SettingKey<string>("a")));
        Assert.Equal("2", collection.GetValue(new SettingKey<string>("b")));
        Assert.Equal("3", collection.GetValue(new SettingKey<string>("c")));
    }

    [Fact]
    public async Task Collection_WhenSingleSource_ContainsAllSettings()
    {
        await using var provider = new ReadOnlySettingCollectionProvider(
            MockDescriptorProvider.Object,
            [DataSource(Setting("a", "1"))]
        );

        Assert.Single(provider.Collection);
    }

    #endregion
}

public class DefaultReadOnlySettingCollectionProviderFactoryTests : BaseTests
{
    [Fact]
    public async Task Create_ReturnsProviderWithExpectedCollection()
    {
        var mockDescriptorProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        var factory = new DefaultReadOnlySettingCollectionProviderFactory(
            mockDescriptorProvider.Object
        );

        var setting = new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] }.Create(
            "1"
        );
        var dataSource = new StaticCollectionDataSource<Setting>([setting]);

        await using var provider = factory.Create([dataSource]);

        Assert.Single(provider.Collection);
        Assert.Equal("1", provider.Collection.GetValue(new SettingKey<string>("a")));
    }
}
