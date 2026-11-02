// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Settings;

public class DefaultSettingDescriptorCollectionTests
{
    private static DefaultSettingDescriptorCollection CreateCollection() =>
        new([
            new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] },
            new SettingDescriptor<int> { Name = "b", AppliesTo = ["client"] },
        ]);

    #region Count / Enumeration Tests

    [Fact]
    public void Count_ReturnsNumberOfDescriptors()
    {
        Assert.Equal(2, CreateCollection().Count);
    }

    [Fact]
    public void GetEnumerator_YieldsAllDescriptors()
    {
        var names = CreateCollection().Select(x => x.Name).OrderBy(x => x).ToList();

        Assert.Equal(["a", "b"], names);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_ByName_WhenPresent_ReturnsTrue()
    {
        var found = CreateCollection().TryGet("a", out var descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal("a", descriptor.Name);
    }

    [Fact]
    public void TryGet_ByName_WhenMissing_ReturnsFalse()
    {
        Assert.False(CreateCollection().TryGet("missing", out _));
    }

    [Fact]
    public void TryGet_ByKey_WhenPresentAndTyped_ReturnsTrue()
    {
        var found = CreateCollection().TryGet(new SettingKey<string>("a"), out var descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal("a", descriptor.Name);
    }

    [Fact]
    public void TryGet_ByKey_WhenWrongType_ReturnsFalse()
    {
        var found = CreateCollection().TryGet(new SettingKey<int>("a"), out var descriptor);

        Assert.False(found);
        Assert.Null(descriptor);
    }

    [Fact]
    public void TryGet_ByKey_WhenMissing_ReturnsFalse()
    {
        var found = CreateCollection()
            .TryGet(new SettingKey<string>("missing"), out var descriptor);

        Assert.False(found);
        Assert.Null(descriptor);
    }

    #endregion
}
