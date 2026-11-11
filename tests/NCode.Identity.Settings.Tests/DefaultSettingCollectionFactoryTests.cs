// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Settings;

public class DefaultSettingCollectionFactoryTests : BaseTests
{
    private DefaultSettingCollectionFactory Factory { get; }

    public DefaultSettingCollectionFactoryTests()
    {
        var mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        Factory = new DefaultSettingCollectionFactory(mockProvider.Object);
    }

    #region Create Tests

    [Fact]
    public void Create_WhenEmpty_ReturnsEmptyCollection()
    {
        var collection = Factory.Create();

        Assert.Empty(collection);
    }

    [Fact]
    public void Create_WithSettings_ReturnsPopulatedCollection()
    {
        var setting = new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] }.Create(
            "1"
        );

        var collection = Factory.Create([setting]);

        Assert.Single(collection);
        Assert.True(collection.TryGetValue("a", out var value));
        Assert.Equal("1", value);
    }

    #endregion
}
