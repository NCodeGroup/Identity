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

public class DefaultSettingCollectionTests : BaseTests
{
    private readonly Mock<ISettingDescriptorCollectionProvider> _mockProvider;

    public DefaultSettingCollectionTests()
    {
        _mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
    }

    private static SettingDescriptor<string> Descriptor(string name, string? defaultValue = null) =>
        new() { Name = name, DefaultOrNull = defaultValue };

    private static Setting<string> Setting(string name, string value) =>
        Descriptor(name).Create(value);

    private DefaultSettingCollection CreateCollection(params Setting[] settings) =>
        new(_mockProvider.Object, settings);

    #region Count / Enumeration Tests

    [Fact]
    public void Count_WhenEmpty_ReturnsZero()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);

        Assert.Empty(collection);
    }

    [Fact]
    public void GetEnumerator_WhenPopulated_YieldsSettings()
    {
        var collection = CreateCollection(Setting("a", "1"), Setting("b", "2"));

        var names = collection.Select(x => x.Descriptor.Name).OrderBy(x => x).ToList();

        Assert.Equal(["a", "b"], names);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_ByName_WhenPresent_ReturnsTrue()
    {
        var setting = Setting("a", "1");
        var collection = CreateCollection(setting);

        var found = collection.TryGet("a", out var result);

        Assert.True(found);
        Assert.Same(setting, result);
    }

    [Fact]
    public void TryGet_ByName_WhenMissing_ReturnsFalse()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);

        Assert.False(collection.TryGet("missing", out _));
    }

    [Fact]
    public void TryGet_ByKey_WhenPresentAndTyped_ReturnsTrue()
    {
        var collection = CreateCollection(Setting("a", "1"));

        var found = collection.TryGet(new SettingKey<string>("a"), out var setting);

        Assert.True(found);
        Assert.Equal("1", setting.Value);
    }

    [Fact]
    public void TryGet_ByKey_WhenWrongType_ReturnsFalse()
    {
        var collection = CreateCollection(Setting("a", "1"));

        var found = collection.TryGet(new SettingKey<int>("a"), out var setting);

        Assert.False(found);
        Assert.Null(setting);
    }

    #endregion

    #region TryGetValue Tests

    [Fact]
    public void TryGetValue_ByName_WhenPresent_ReturnsBoxedValue()
    {
        var collection = CreateCollection(Setting("a", "1"));

        var found = collection.TryGetValue("a", out var value);

        Assert.True(found);
        Assert.Equal("1", value);
    }

    [Fact]
    public void TryGetValue_ByName_WhenMissing_ReturnsFalse()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);

        Assert.False(collection.TryGetValue("missing", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValue_ByKey_WhenPresent_ReturnsTypedValue()
    {
        var collection = CreateCollection(Setting("a", "1"));

        var found = collection.TryGetValue(new SettingKey<string>("a"), out var value);

        Assert.True(found);
        Assert.Equal("1", value);
    }

    [Fact]
    public void TryGetValue_ByKey_WhenMissing_ReturnsFalse()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);

        Assert.False(collection.TryGetValue(new SettingKey<string>("missing"), out var value));
        Assert.Null(value);
    }

    #endregion

    #region GetValue Tests

    [Fact]
    public void GetValue_WhenPresent_ReturnsValue()
    {
        var collection = CreateCollection(Setting("a", "1"));

        Assert.Equal("1", collection.GetValue(new SettingKey<string>("a")));
    }

    [Fact]
    public void GetValue_WhenMissingButDescriptorHasDefault_ReturnsDefault()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);
        var mockDescriptorCollection = CreateStrictMock<ISettingDescriptorCollection>();
        SettingDescriptor<string>? descriptor = Descriptor("a", "fallback");
        mockDescriptorCollection
            .Setup(x => x.TryGet(It.IsAny<SettingKey<string>>(), out descriptor))
            .Returns(true)
            .Verifiable();
        _mockProvider
            .Setup(x => x.Collection)
            .Returns(mockDescriptorCollection.Object)
            .Verifiable();

        var value = collection.GetValue(new SettingKey<string>("a"));

        Assert.Equal("fallback", value);
    }

    [Fact]
    public void GetValue_WhenMissingAndNoDefault_ThrowsKeyNotFound()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);
        var mockDescriptorCollection = CreateStrictMock<ISettingDescriptorCollection>();
        SettingDescriptor<string>? descriptor = null;
        mockDescriptorCollection
            .Setup(x => x.TryGet(It.IsAny<SettingKey<string>>(), out descriptor))
            .Returns(false)
            .Verifiable();
        _mockProvider
            .Setup(x => x.Collection)
            .Returns(mockDescriptorCollection.Object)
            .Verifiable();

        Assert.Throws<KeyNotFoundException>(() => collection.GetValue(new SettingKey<string>("a")));
    }

    #endregion

    #region Set Tests

    [Fact]
    public void Set_Setting_StoresByName()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);

        collection.Set(Setting("a", "1"));

        Assert.True(collection.TryGetValue("a", out var value));
        Assert.Equal("1", value);
    }

    [Fact]
    public void Set_ByKey_WhenDescriptorExists_UsesDescriptor()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);
        var mockDescriptorCollection = CreateStrictMock<ISettingDescriptorCollection>();
        SettingDescriptor<string>? descriptor = Descriptor("a");
        mockDescriptorCollection
            .Setup(x => x.TryGet(It.IsAny<SettingKey<string>>(), out descriptor))
            .Returns(true)
            .Verifiable();
        _mockProvider
            .Setup(x => x.Collection)
            .Returns(mockDescriptorCollection.Object)
            .Verifiable();

        collection.Set(new SettingKey<string>("a"), "1");

        Assert.Equal("1", collection.GetValue(new SettingKey<string>("a")));
    }

    [Fact]
    public void Set_ByKey_WhenDescriptorMissing_CreatesDefaultDescriptor()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);
        var mockDescriptorCollection = CreateStrictMock<ISettingDescriptorCollection>();
        SettingDescriptor<string>? descriptor = null;
        mockDescriptorCollection
            .Setup(x => x.TryGet(It.IsAny<SettingKey<string>>(), out descriptor))
            .Returns(false)
            .Verifiable();
        _mockProvider
            .Setup(x => x.Collection)
            .Returns(mockDescriptorCollection.Object)
            .Verifiable();

        collection.Set(new SettingKey<string>("a"), "1");

        Assert.Equal("1", collection.GetValue(new SettingKey<string>("a")));
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_WhenPresent_RemovesAndReturnsTrue()
    {
        var collection = CreateCollection(Setting("a", "1"));

        var removed = collection.Remove(new SettingKey<string>("a"));

        Assert.True(removed);
        Assert.Empty(collection);
    }

    [Fact]
    public void Remove_WhenMissing_ReturnsFalse()
    {
        var collection = new DefaultSettingCollection(_mockProvider.Object);

        Assert.False(collection.Remove(new SettingKey<string>("missing")));
    }

    #endregion

    #region Merge Tests

    [Fact]
    public void Merge_CombinesSettings_OtherWins()
    {
        var collection = CreateCollection(Setting("a", "current"), Setting("b", "keep"));

        var merged = collection.Merge([Setting("a", "other"), Setting("c", "new")]);

        Assert.Equal(3, merged.Count);
        Assert.Equal("other", merged.GetValue(new SettingKey<string>("a")));
        Assert.Equal("keep", merged.GetValue(new SettingKey<string>("b")));
        Assert.Equal("new", merged.GetValue(new SettingKey<string>("c")));
    }

    #endregion
}
