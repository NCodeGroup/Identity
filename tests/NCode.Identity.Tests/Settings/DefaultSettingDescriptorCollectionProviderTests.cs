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
                    new SettingDescriptor<string> { Name = "a" },
                    new SettingDescriptor<int> { Name = "b" }
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
            [DataSource(new SettingDescriptor<string> { Name = "a" })]
        );

        Assert.NotNull(provider.GetChangeToken());
    }

    #endregion
}
