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
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class EnumAndJsonParameterParserTests : BaseTests
{
    private readonly OpenIdEnvironment _environment;
    private readonly ParameterDescriptor _allowMissingDescriptor = new(
        "test",
        ParameterLoader.Default
    );

    public enum SampleColor
    {
        Red,
        DarkGreen,
    }

    [Flags]
    public enum SampleAccess
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    private sealed record SamplePayload(string Name, int Age);

    public EnumAndJsonParameterParserTests()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        mockEnvironment.Setup(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockEnvironment
            .Setup(x => x.JsonSerializerOptions)
            .Returns(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        _environment = mockEnvironment.Object;
    }

    private ParameterDescriptor RequiredDescriptor<T>(IParameterParser<T> parser) =>
        new(new KnownParameter<T>("test", parser) { AllowMissingStringValues = false });

    #region EnumParser Tests

    [Theory]
    [InlineData(SampleColor.Red, "red")]
    [InlineData(SampleColor.DarkGreen, "dark_green")]
    public void EnumParser_GetStringValues_ReturnsSnakeCaseName(SampleColor value, string expected)
    {
        var result = EnumParser<SampleColor>.Singleton.GetStringValues(
            _environment,
            _allowMissingDescriptor,
            value
        );

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("red", SampleColor.Red)]
    [InlineData("dark_green", SampleColor.DarkGreen)]
    [InlineData("DARK_GREEN", SampleColor.DarkGreen)]
    public void EnumParser_Parse_ReturnsEnum(string input, SampleColor expected)
    {
        var result = EnumParser<SampleColor>.Singleton.Parse(
            _environment,
            _allowMissingDescriptor,
            input
        );

        Assert.Equal(expected, result);
    }

    [Fact]
    public void EnumParser_Parse_WhenMissingAndAllowed_ReturnsDefault()
    {
        var result = EnumParser<SampleColor>.Singleton.Parse(
            _environment,
            _allowMissingDescriptor,
            StringValues.Empty
        );

        Assert.Equal(default, result);
    }

    [Fact]
    public void EnumParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = EnumParser<SampleColor>.Singleton;

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    [Fact]
    public void EnumParser_Parse_WhenTooManyValuesAndNotFlags_Throws()
    {
        var parser = EnumParser<SampleColor>.Singleton;

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(
                _environment,
                _allowMissingDescriptor,
                new StringValues(["red", "dark_green"])
            )
        );
    }

    [Fact]
    public void EnumParser_Parse_WhenInvalid_Throws()
    {
        var parser = EnumParser<SampleColor>.Singleton;

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, "not-a-color")
        );
    }

    [Fact]
    public void EnumParser_Flags_GetStringValues_ReturnsEachFlag()
    {
        var result = EnumParser<SampleAccess>.Singleton.GetStringValues(
            _environment,
            _allowMissingDescriptor,
            SampleAccess.Read | SampleAccess.Write
        );

        Assert.Contains("read", result.ToArray());
        Assert.Contains("write", result.ToArray());
    }

    [Fact]
    public void EnumParser_Flags_Parse_CombinesValues()
    {
        var result = EnumParser<SampleAccess>.Singleton.Parse(
            _environment,
            _allowMissingDescriptor,
            new StringValues(["read", "write"])
        );

        Assert.Equal(SampleAccess.Read | SampleAccess.Write, result);
    }

    #endregion

    #region JsonParser Tests

    [Fact]
    public void JsonParser_GetStringValues_SerializesToJson()
    {
        var parser = new JsonParser<SamplePayload>();

        var result = parser.GetStringValues(
            _environment,
            _allowMissingDescriptor,
            new SamplePayload("alice", 30)
        );

        var single = Assert.Single(result.ToArray());
        Assert.Contains("alice", single);
    }

    [Fact]
    public void JsonParser_Parse_DeserializesFromJson()
    {
        var parser = new JsonParser<SamplePayload>();

        var result = parser.Parse(
            _environment,
            _allowMissingDescriptor,
            """{"name":"alice","age":30}"""
        );

        Assert.NotNull(result);
        Assert.Equal("alice", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void JsonParser_Parse_WhenMissingAndAllowed_ReturnsDefault()
    {
        var parser = new JsonParser<SamplePayload>();

        var result = parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void JsonParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = new JsonParser<SamplePayload>();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    [Fact]
    public void JsonParser_Parse_WhenTooManyValues_Throws()
    {
        var parser = new JsonParser<SamplePayload>();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, new StringValues(["{}", "{}"]))
        );
    }

    [Fact]
    public void JsonParser_Parse_WhenInvalidJson_Throws()
    {
        var parser = new JsonParser<SamplePayload>();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, "not json")
        );
    }

    #endregion
}
