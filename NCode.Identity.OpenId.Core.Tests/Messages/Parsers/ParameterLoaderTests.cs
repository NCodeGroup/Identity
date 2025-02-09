#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

public class ParameterLoaderTests : BaseTests
{
    private Mock<ParameterLoader> MockParameterLoader { get; }
    private ParameterLoader ParameterLoader { get; }

    public ParameterLoaderTests()
    {
        MockParameterLoader = CreatePartialMock<ParameterLoader>();
        ParameterLoader = MockParameterLoader.Object;
    }

    [Fact]
    public void GetValueToSerialize_FormatJson_Valid()
    {
        const SerializationFormat format = SerializationFormat.Json;
        const string parsedValue = nameof(parsedValue);

        var mockParameter = CreateStrictMock<IParameter>();

        mockParameter
            .Setup(x => x.GetParsedValue())
            .Returns(parsedValue)
            .Verifiable();

        var result = ParameterLoader.GetValueToSerialize(mockParameter.Object, format);
        Assert.Equal(parsedValue, result);
    }

    [Fact]
    public void GetValueToSerialize_FormatOpenId_Valid()
    {
        const SerializationFormat format = SerializationFormat.OpenId;
        const string openIdValue = nameof(openIdValue);

        StringValues stringValues = new[] { "stringValue1", "stringValue2" };

        MockParameterLoader
            .Setup(x => x.FromStringValues(stringValues))
            .Returns(openIdValue)
            .Verifiable();

        var mockParameter = CreateStrictMock<IParameter>();
        mockParameter
            .Setup(x => x.StringValues)
            .Returns(stringValues)
            .Verifiable();

        var result = ParameterLoader.GetValueToSerialize(mockParameter.Object, format);
        Assert.Equal(openIdValue, result);
    }

    [Fact]
    public void FromStringValues_GivenEmpty_Valid()
    {
        var stringValues = StringValues.Empty;
        var result = ParameterLoader.FromStringValues(stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void FromStringValues_GivenSingle_Valid()
    {
        const string stringValue = nameof(stringValue);
        StringValues stringValues = new[] { stringValue };
        var result = ParameterLoader.FromStringValues(stringValues);
        Assert.Equal(stringValue, result);
    }

    [Fact]
    public void FromStringValues_GivenMultiple_Valid()
    {
        var stringValuesArray = new[] { "stringValue1", "stringValue2" };
        StringValues stringValues = stringValuesArray;
        var result = ParameterLoader.FromStringValues(stringValues);
        Assert.Equal(string.Join(' ', stringValuesArray), result);
    }
}
