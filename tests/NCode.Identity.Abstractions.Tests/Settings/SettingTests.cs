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

public class SettingTests
{
    private static SettingDescriptor<string> CreateDescriptor(string name = "example") =>
        new() { Name = name };

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
