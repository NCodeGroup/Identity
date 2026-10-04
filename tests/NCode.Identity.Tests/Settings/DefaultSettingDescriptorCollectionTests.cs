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

public class DefaultSettingDescriptorCollectionTests
{
    private static DefaultSettingDescriptorCollection CreateCollection() =>
        new([
            new SettingDescriptor<string> { Name = "a" },
            new SettingDescriptor<int> { Name = "b" },
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
