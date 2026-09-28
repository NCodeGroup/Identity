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

using System.Text.Json;

namespace NCode.Identity.Settings;

public class DefaultSettingSerializerTests : BaseTests
{
    private readonly Mock<ISettingDescriptorJsonProvider> _mockJsonProvider;
    private readonly DefaultSettingSerializer _serializer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public DefaultSettingSerializerTests()
    {
        _mockJsonProvider = CreateStrictMock<ISettingDescriptorJsonProvider>();
        _serializer = new DefaultSettingSerializer(_mockJsonProvider.Object);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    #region DeserializeSettings Tests

    [Fact]
    public void DeserializeSettings_WhenNull_ReturnsEmpty()
    {
        var result = _serializer.DeserializeSettings(Parse("null"), _jsonOptions);

        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeSettings_WhenNotObject_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            _serializer.DeserializeSettings(Parse("[]"), _jsonOptions)
        );
    }

    [Fact]
    public void DeserializeSettings_WhenObject_CreatesSettingsFromValues()
    {
        _mockJsonProvider
            .Setup(x => x.GetDescriptor("a", JsonValueKind.String))
            .Returns(new SettingDescriptor<string> { Name = "a" })
            .Verifiable();

        var result = _serializer.DeserializeSettings(Parse("""{"a":"value"}"""), _jsonOptions);

        var setting = Assert.Single(result);
        Assert.Equal("a", setting.Descriptor.Name);
        Assert.Equal("value", setting.GetValue());
    }

    [Fact]
    public void DeserializeSettings_WhenNullValueAndDescriptorHasDefault_UsesDefault()
    {
        _mockJsonProvider
            .Setup(x => x.GetDescriptor("a", JsonValueKind.Null))
            .Returns(new SettingDescriptor<string> { Name = "a", Default = "fallback" })
            .Verifiable();

        var result = _serializer.DeserializeSettings(Parse("""{"a":null}"""), _jsonOptions);

        var setting = Assert.Single(result);
        Assert.Equal("fallback", setting.GetValue());
    }

    [Fact]
    public void DeserializeSettings_WhenNullValueAndNoDefault_ThrowsJsonException()
    {
        _mockJsonProvider
            .Setup(x => x.GetDescriptor("a", JsonValueKind.Null))
            .Returns(new SettingDescriptor<string> { Name = "a" })
            .Verifiable();

        Assert.Throws<JsonException>(() =>
            _serializer.DeserializeSettings(Parse("""{"a":null}"""), _jsonOptions)
        );
    }

    #endregion
}
