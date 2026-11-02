// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Settings;

public class SettingTests
{
    private static SettingDescriptor<string> CreateDescriptor(string name = "example") =>
        new() { Name = name, AppliesTo = ["client"] };

    #region Setting<TValue> Tests

    [Fact]
    public void Value_WhenConstructed_RoundTrips()
    {
        var descriptor = CreateDescriptor();

        var setting = new Setting<string>(descriptor, "hello");

        Assert.Equal("hello", setting.Value);
    }

    [Fact]
    public void Descriptor_WhenConstructed_ReturnsTypedDescriptor()
    {
        var descriptor = CreateDescriptor();

        var setting = new Setting<string>(descriptor, "hello");

        Assert.Same(descriptor, setting.Descriptor);
    }

    [Fact]
    public void GetValue_WhenCalled_ReturnsBoxedValue()
    {
        var descriptor = CreateDescriptor();

        var setting = new Setting<string>(descriptor, "hello");

        Assert.Equal("hello", setting.GetValue());
    }

    [Fact]
    public void BaseDescriptor_WhenAccessedViaBase_ReturnsSameDescriptor()
    {
        var descriptor = CreateDescriptor();

        Setting setting = new Setting<string>(descriptor, "hello");

        Assert.Same(descriptor, setting.Descriptor);
    }

    #endregion
}
