#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

namespace NCode.Identity.Settings;

public class ReadOnlySettingCollectionProviderTests : BaseTests
{
    private readonly Mock<ISettingDescriptorCollectionProvider> _mockDescriptorProvider;

    public ReadOnlySettingCollectionProviderTests()
    {
        _mockDescriptorProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
    }

    private static Setting<string> Setting(string name, string value) =>
        new SettingDescriptor<string> { Name = name }.Create(value);

    private static ICollectionDataSource<Setting> DataSource(params Setting[] settings) =>
        new StaticCollectionDataSource<Setting>(settings);

    #region Collection Tests

    [Fact]
    public async Task Collection_MergesDataSources_LastValueWins()
    {
        await using var provider = new ReadOnlySettingCollectionProvider(
            _mockDescriptorProvider.Object,
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
            _mockDescriptorProvider.Object,
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

        var setting = new SettingDescriptor<string> { Name = "a" }.Create("1");
        var dataSource = new StaticCollectionDataSource<Setting>([setting]);

        await using var provider = factory.Create([dataSource]);

        Assert.Single(provider.Collection);
        Assert.Equal("1", provider.Collection.GetValue(new SettingKey<string>("a")));
    }
}
