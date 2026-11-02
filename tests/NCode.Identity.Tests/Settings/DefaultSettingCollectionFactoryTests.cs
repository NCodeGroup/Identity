// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        var setting = new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] }.Create(
            "1"
        );

        var collection = _factory.Create([setting]);

        Assert.Single(collection);
        Assert.True(collection.TryGetValue("a", out var value));
        Assert.Equal("1", value);
    }

    #endregion
}
