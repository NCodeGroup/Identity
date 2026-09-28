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

public class SettingDescriptorTests
{
    #region ValueType Property Tests

    [Fact]
    public void ValueType_WhenStringValue_ReturnsStringType()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        Assert.Equal(typeof(string), descriptor.ValueType);
    }

    #endregion

    #region HasDefault Property Tests

    [Fact]
    public void HasDefault_WhenDefaultNotSet_ReturnsFalse()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        Assert.False(descriptor.HasDefault);
    }

    [Fact]
    public void HasDefault_WhenDefaultSet_ReturnsTrue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", Default = "value" };

        Assert.True(descriptor.HasDefault);
    }

    #endregion

    #region Default Property Tests

    [Fact]
    public void Default_WhenNotSet_Throws()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        Assert.Throws<InvalidOperationException>(() => descriptor.Default);
    }

    [Fact]
    public void Default_WhenSet_ReturnsValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", Default = "value" };

        Assert.Equal("value", descriptor.Default);
        Assert.Equal("value", descriptor.DefaultOrNull);
    }

    #endregion

    #region Key Property Tests

    [Fact]
    public void Key_WhenAccessed_ReturnsKeyWithName()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        var key = descriptor.Key;

        Assert.Equal("example", key.SettingName);
        Assert.Equal(typeof(string), key.ValueType);
    }

    [Fact]
    public void ImplicitConversion_ToSettingKey_UsesName()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        SettingKey<string> key = descriptor;

        Assert.Equal("example", key.SettingName);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithTypedValue_ReturnsSettingWithValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        var setting = descriptor.Create("value");

        Assert.Equal("value", setting.Value);
        Assert.Same(descriptor, setting.Descriptor);
    }

    [Fact]
    public void Create_WithBoxedValue_ReturnsSettingWithValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        var setting = descriptor.Create((object)"value");

        Assert.Equal("value", setting.GetValue());
    }

    [Fact]
    public void CreateDefault_WhenDefaultSet_ReturnsSettingWithDefault()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", Default = "value" };

        var setting = descriptor.CreateDefault();

        Assert.Equal("value", setting.GetValue());
    }

    [Fact]
    public void CreateDefault_WhenNoDefault_Throws()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };

        Assert.Throws<InvalidOperationException>(() => descriptor.CreateDefault());
    }

    #endregion

    #region Merge Tests

    [Fact]
    public void Merge_WhenDefaultBehavior_ReturnsOtherValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };
        var current = descriptor.Create("current");
        var other = descriptor.Create("other");

        var merged = descriptor.Merge(current, other);

        Assert.Equal("other", merged.Value);
    }

    [Fact]
    public void Merge_WhenCustomOnMerge_UsesCustomLogic()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            OnMerge = (current, other) => current + other,
        };
        var current = descriptor.Create("a");
        var other = descriptor.Create("b");

        var merged = descriptor.Merge(current, other);

        Assert.Equal("ab", merged.Value);
    }

    [Fact]
    public void Merge_WithBoxedSettings_ReturnsOtherValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };
        Setting current = descriptor.Create("current");
        Setting other = descriptor.Create("other");

        var merged = descriptor.Merge(current, other);

        Assert.Equal("other", merged.GetValue());
    }

    #endregion

    #region Format Tests

    [Fact]
    public void Format_WhenDefaultBehavior_ReturnsValueAsIs()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };
        var setting = descriptor.Create("value");

        var formatted = descriptor.Format(setting);

        Assert.Equal("value", formatted);
    }

    [Fact]
    public void Format_WhenCustomOnFormat_UsesCustomLogic()
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = "example",
            OnFormat = setting => setting.Value.ToUpperInvariant(),
        };
        var setting = descriptor.Create("value");

        var formatted = descriptor.Format(setting);

        Assert.Equal("VALUE", formatted);
    }

    [Fact]
    public void Format_WithBoxedSetting_ReturnsValue()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example" };
        Setting setting = descriptor.Create("value");

        var formatted = descriptor.Format(setting);

        Assert.Equal("value", formatted);
    }

    #endregion

    #region IsDiscoverable Property Tests

    [Fact]
    public void IsDiscoverable_WhenSet_RoundTrips()
    {
        var descriptor = new SettingDescriptor<string> { Name = "example", IsDiscoverable = true };

        Assert.True(descriptor.IsDiscoverable);
    }

    #endregion
}
