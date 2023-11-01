#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Moq;
using NIdentity.OpenId.Settings;
using Xunit;
using Xunit.Abstractions;

namespace NIdentity.OpenId.Core.Tests.Settings;

public class SettingJsonConverterTests : BaseTests
{
    private ITestOutputHelper Output { get; }
    private Mock<IJsonSettingDescriptorProvider> MockJsonSettingDescriptorProvider { get; }
    private SettingJsonConverter Converter { get; }
    private JsonSerializerOptions JsonSerializerOptions { get; }

    public SettingJsonConverterTests(ITestOutputHelper output)
    {
        Output = output;

        MockJsonSettingDescriptorProvider = CreatePartialMock<IJsonSettingDescriptorProvider>();
        Converter = new SettingJsonConverter(MockJsonSettingDescriptorProvider.Object);

        JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { Converter }
        };
    }

    private readonly struct SettingValue
    {
        public required string StringValue { get; init; }
        public required DateTimeOffset DateTimeOffsetValue { get; init; }
        public required IList<string> StringListValue { get; init; }
    }

    private readonly struct SettingEnvelope<TValue>
    {
        public required string Name { get; init; }
        public required TValue Value { get; init; }
    }

    [Fact]
    public void Serialize()
    {
        const string settingName = nameof(settingName);

        var descriptor = new SettingDescriptor<SettingValue>
        {
            SettingName = settingName,
            OnMerge = (_, other) => other
        };

        var settingValue = new SettingValue
        {
            StringValue = "stringValue",
            DateTimeOffsetValue = DateTimeOffset.Now,
            StringListValue = new List<string> { "One", "Two" }
        };
        var setting = new Setting<SettingValue>(descriptor, settingValue);

        var json = JsonSerializer.Serialize(setting, JsonSerializerOptions);
        Output.WriteLine(json);

        var envelope = JsonSerializer.Deserialize<SettingEnvelope<SettingValue>>(json, JsonSerializerOptions);

        Assert.Equal(settingName, envelope.Name);
        Assert.Equal(settingValue.StringValue, envelope.Value.StringValue);
        Assert.Equal(settingValue.DateTimeOffsetValue, envelope.Value.DateTimeOffsetValue);
        Assert.Equal(settingValue.StringListValue, envelope.Value.StringListValue);
    }

    [Fact]
    public void RoundTrip()
    {
        const string settingName = nameof(settingName);

        var descriptor = new SettingDescriptor<SettingValue>
        {
            SettingName = settingName,
            OnMerge = (_, other) => other
        };

        var settingValue = new SettingValue
        {
            StringValue = "stringValue",
            DateTimeOffsetValue = DateTimeOffset.Now,
            StringListValue = new List<string> { "One", "Two" }
        };
        var setting = new Setting<SettingValue>(descriptor, settingValue);

        var json = JsonSerializer.Serialize(setting, JsonSerializerOptions);
        Output.WriteLine(json);

        MockJsonSettingDescriptorProvider
            .Setup(x => x.GetDescriptor(settingName, JsonTokenType.StartObject))
            .Returns(descriptor)
            .Verifiable();

        var result = JsonSerializer.Deserialize<Setting>(json, JsonSerializerOptions);

        Assert.NotNull(result);
        Assert.Same(descriptor, result.BaseDescriptor);

        var resultValue = (SettingValue)result.GetValue();
        Assert.Equal(settingValue.StringValue, resultValue.StringValue);
        Assert.Equal(settingValue.DateTimeOffsetValue, resultValue.DateTimeOffsetValue);
        Assert.Equal(settingValue.StringListValue, resultValue.StringListValue);
    }
}
