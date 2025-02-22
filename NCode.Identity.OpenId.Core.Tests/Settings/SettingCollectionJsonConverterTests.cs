﻿#region Copyright Preamble

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
using System.Text.Json.Serialization;
using Moq;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Settings;

public class SettingCollectionJsonConverterTests : BaseTests
{
    private Mock<ISettingDescriptorJsonProvider> MockSettingDescriptorJsonProvider { get; }
    private SettingCollectionJsonConverter Converter { get; }
    private JsonSerializerOptions JsonSerializerOptions { get; }

    private static readonly string[] ListStringValue = ["one", "two"];
    private static readonly string[] ListEnumValue = ["base64", "hex"];

    public SettingCollectionJsonConverterTests()
    {
        MockSettingDescriptorJsonProvider = CreatePartialMock<ISettingDescriptorJsonProvider>();
        Converter = new SettingCollectionJsonConverter(MockSettingDescriptorJsonProvider.Object);

        JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            Converters = { Converter, new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };
    }

    [Fact]
    public void RoundTrip()
    {
        var descriptorScalarString = new SettingDescriptor<string>
        {
            Name = "scalar_string_setting",
            OnMerge = (_, other) => other
        };

        var descriptorScalarNumber = new SettingDescriptor<double>
        {
            Name = "scalar_double_setting",
            OnMerge = (_, other) => other
        };

        var descriptorScalarBoolean = new SettingDescriptor<bool>
        {
            Name = "scalar_bool_setting",
            OnMerge = (_, other) => other
        };

        var descriptorScalarDateTimeOffset = new SettingDescriptor<DateTimeOffset>
        {
            Name = "scalar_datetimeoffset_setting",
            OnMerge = (_, other) => other
        };

        var descriptorListString = new SettingDescriptor<IReadOnlyCollection<string>>
        {
            Name = "list_string_setting",
            OnMerge = (_, other) => other
        };

        var descriptorListEnum = new SettingDescriptor<IReadOnlyCollection<BinaryEncodingType>>
        {
            Name = "list_enum_setting",
            OnMerge = (_, other) => other
        };

        var now = DateTimeOffset.Now;
        var settings = new List<Setting>
        {
            descriptorScalarString.Create("scalar_string_value"),
            descriptorScalarNumber.Create(3.14),
            descriptorScalarBoolean.Create(true),
            descriptorScalarDateTimeOffset.Create(now),
            descriptorListString.Create(ListStringValue),
            descriptorListEnum.Create([BinaryEncodingType.Base64, BinaryEncodingType.Hex])
        };

        var expected = JsonSerializer.Serialize(new
        {
            scalar_string_setting = "scalar_string_value",
            scalar_double_setting = 3.14,
            scalar_bool_setting = true,
            scalar_datetimeoffset_setting = now,
            list_string_setting = ListStringValue,
            list_enum_setting = ListEnumValue
        });

        var json = JsonSerializer.Serialize(settings, JsonSerializerOptions);
        Assert.Equal(expected, json);

        MockSettingDescriptorJsonProvider
            .Setup(x => x.GetDescriptor(descriptorScalarString.Name, JsonTokenType.String))
            .Returns(descriptorScalarString)
            .Verifiable();

        MockSettingDescriptorJsonProvider
            .Setup(x => x.GetDescriptor(descriptorScalarNumber.Name, JsonTokenType.Number))
            .Returns(descriptorScalarNumber)
            .Verifiable();

        MockSettingDescriptorJsonProvider
            .Setup(x => x.GetDescriptor(descriptorScalarBoolean.Name, JsonTokenType.True))
            .Returns(descriptorScalarBoolean)
            .Verifiable();

        MockSettingDescriptorJsonProvider
            .Setup(x => x.GetDescriptor(descriptorScalarDateTimeOffset.Name, JsonTokenType.String))
            .Returns(descriptorScalarDateTimeOffset)
            .Verifiable();

        MockSettingDescriptorJsonProvider
            .Setup(x => x.GetDescriptor(descriptorListString.Name, JsonTokenType.StartArray))
            .Returns(descriptorListString)
            .Verifiable();

        MockSettingDescriptorJsonProvider
            .Setup(x => x.GetDescriptor(descriptorListEnum.Name, JsonTokenType.StartArray))
            .Returns(descriptorListEnum)
            .Verifiable();

        var results = JsonSerializer.Deserialize<IList<Setting>>(json, JsonSerializerOptions);
        Assert.Equal(settings, results, AreSettingsEqual);
    }

    private static bool AreSettingsEqual(Setting x, Setting y)
    {
        var xJson = JsonSerializer.Serialize(new { x.Descriptor.Name, Value = x.GetValue() });
        var yJson = JsonSerializer.Serialize(new { y.Descriptor.Name, Value = y.GetValue() });
        return xJson == yJson;
    }
}
