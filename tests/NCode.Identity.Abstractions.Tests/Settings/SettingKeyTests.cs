// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Settings;

public class SettingKeyTests
{
    #region ValueType Property Tests

    [Fact]
    public void ValueType_WhenStringValue_ReturnsStringType()
    {
        var key = new SettingKey<string>("example");

        Assert.Equal(typeof(string), key.ValueType);
    }

    [Fact]
    public void ValueType_WhenIntValue_ReturnsInt32Type()
    {
        var key = new SettingKey<int>("example");

        Assert.Equal(typeof(int), key.ValueType);
    }

    #endregion

    #region SettingName Property Tests

    [Fact]
    public void SettingName_WhenSet_RoundTrips()
    {
        var key = new SettingKey<string>("my-setting");

        Assert.Equal("my-setting", key.SettingName);
    }

    [Fact]
    public void Equality_WhenSameNameAndType_AreEqual()
    {
        var left = new SettingKey<string>("name");
        var right = new SettingKey<string>("name");

        Assert.Equal(left, right);
    }

    #endregion
}
