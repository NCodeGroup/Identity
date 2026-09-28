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

namespace NCode.Identity.Settings;

public class DefaultSettingCollectionFactoryTests : BaseTests
{
    private readonly DefaultSettingCollectionFactory _factory;

    public DefaultSettingCollectionFactoryTests()
    {
        var mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        _factory = new DefaultSettingCollectionFactory(mockProvider.Object);
    }

    #region Create Tests

    [Fact]
    public void Create_WhenEmpty_ReturnsEmptyCollection()
    {
        var collection = _factory.Create();

        Assert.Empty(collection);
    }

    [Fact]
    public void Create_WithSettings_ReturnsPopulatedCollection()
    {
        var setting = new SettingDescriptor<string> { Name = "a" }.Create("1");

        var collection = _factory.Create([setting]);

        Assert.Single(collection);
        Assert.True(collection.TryGetValue("a", out var value));
        Assert.Equal("1", value);
    }

    #endregion
}
