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
