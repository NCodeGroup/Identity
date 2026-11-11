// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Serialization;
using Xunit;
using OpenIdUriParser = NCode.Identity.OpenId.Messages.Parsers.UriParser;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class AdditionalParameterParserTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private OpenIdEnvironment Environment { get; }
    private ParameterDescriptor AllowMissingDescriptor { get; } =
        new("test", ParameterLoader.Default);

    public AdditionalParameterParserTests()
    {
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        Environment = MockEnvironment.Object;
    }

    // Only a parser that refuses its input reads the environment's error factory.
    private void SetupErrorFactory()
    {
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        MockEnvironment.Setup(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    private static ParameterDescriptor RequiredDescriptor<T>(IParameterParser<T> parser) =>
        new(new KnownParameter<T>("test", parser) { AllowMissingStringValues = false });

    #region StringParser Tests

    [Fact]
    public void StringParser_GetStringValues_ReturnsValue()
    {
        var parser = new StringParser();

        Assert.Equal("value", parser.GetStringValues(Environment, AllowMissingDescriptor, "value"));
    }

    [Fact]
    public void StringParser_Parse_ReturnsSingleValue()
    {
        var parser = new StringParser();

        Assert.Equal("value", parser.Parse(Environment, AllowMissingDescriptor, "value"));
    }

    [Fact]
    public void StringParser_Parse_WhenMissingAndAllowed_ReturnsNull()
    {
        var parser = new StringParser();

        Assert.Null(parser.Parse(Environment, AllowMissingDescriptor, StringValues.Empty));
    }

    [Fact]
    public void StringParser_Parse_WhenMissingAndRequired_Throws()
    {
        SetupErrorFactory();
        var parser = new StringParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(Environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    [Fact]
    public void StringParser_Parse_WhenTooManyValues_Throws()
    {
        SetupErrorFactory();
        var parser = new StringParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(Environment, AllowMissingDescriptor, new StringValues(["a", "b"]))
        );
    }

    [Theory]
    [InlineData("1712345678", "1712345678")]
    [InlineData("12.5", "12.5")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    public void StringParser_Read_WhenOpenIdFormatAndJsonScalar_ReadsItsStringForm(
        string json,
        string expected
    )
    {
        // A request object (RFC 9101) carries max_age and the JWT's iat/exp as JSON numbers.
        var parser = new StringParser();
        var reader = new System.Text.Json.Utf8JsonReader(SecureEncoding.UTF8.GetBytes(json));
        reader.Read();

        var parameter = parser.Read(
            ref reader,
            Environment,
            AllowMissingDescriptor,
            SerializationFormat.OpenId,
            new System.Text.Json.JsonSerializerOptions()
        );

        Assert.Equal(expected, Assert.IsAssignableFrom<IParameter<string>>(parameter).ParsedValue);
    }

    [Fact]
    public void StringListParser_Read_WhenOpenIdFormatAndJsonArray_ReadsEachElement()
    {
        // The 'aud' claim of a request object (RFC 7519) may be an array of strings.
        var parser = new StringListParser();
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new StringValuesJsonConverter() },
        };
        var reader = new System.Text.Json.Utf8JsonReader(
            SecureEncoding.UTF8.GetBytes("[\"https://op\",\"https://other\"]")
        );
        reader.Read();

        var parameter = parser.Read(
            ref reader,
            Environment,
            AllowMissingDescriptor,
            SerializationFormat.OpenId,
            options
        );

        var parsedValue = Assert.IsAssignableFrom<IParameter<List<string>>>(parameter).ParsedValue;
        Assert.Equal(["https://op", "https://other"], parsedValue);
    }

    [Fact]
    public void Read_WhenMessageHasArrayClaim_KeepsEveryValue()
    {
        var scopesParameter = OpenIdCommonParameters.Scopes;

        MockEnvironment
            .Setup(x => x.GetParameterDescriptor(It.IsAny<string>()))
            .Returns(
                (string name) =>
                    name == scopesParameter.Name
                        ? new ParameterDescriptor(scopesParameter)
                        : new ParameterDescriptor(name, ParameterLoader.Default)
            )
            .Verifiable();
        MockEnvironment
            .Setup(x => x.CreateMessage("OpenIdMessage", It.IsAny<IEnumerable<IParameter>>()))
            .Returns(
                (string _, IEnumerable<IParameter> parameters) =>
                    new OpenIdMessage(Environment, parameters)
            )
            .Verifiable();
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Converters =
            {
                new StringValuesJsonConverter(),
                new OpenIdMessageJsonConverterFactory(Environment),
            },
        };

        var message = System.Text.Json.JsonSerializer.Deserialize<OpenIdMessage>(
            "{\"aud\":[\"https://op\",\"https://other\"],\"scope\":\"openid\"}",
            options
        );

        Assert.NotNull(message);
        Assert.True(message.Parameters.TryGet("aud", out var audience));
        var audienceValues = audience.GetStringValues(Environment);
        var scopes = message.Parameters.GetValueOrDefault(scopesParameter);
        Assert.Equal(new StringValues(["https://op", "https://other"]), audienceValues);
        Assert.Equal(["openid"], scopes);
    }

    #endregion

    #region StringListParser Tests

    [Fact]
    public void StringListParser_GetStringValues_WhenNull_ReturnsEmpty()
    {
        var parser = new StringListParser();

        Assert.Equal(
            StringValues.Empty,
            parser.GetStringValues(Environment, AllowMissingDescriptor, null)
        );
    }

    [Fact]
    public void StringListParser_Parse_SplitsOnSeparator()
    {
        var parser = new StringListParser();
        var input =
            $"a{OpenIdProtocol.ParameterSeparatorChar}b{OpenIdProtocol.ParameterSeparatorChar}c";

        var result = parser.Parse(Environment, AllowMissingDescriptor, input);

        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void StringListParser_Parse_WhenMissingAndRequired_Throws()
    {
        SetupErrorFactory();
        var parser = new StringListParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(Environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    #endregion

    #region UriParser Tests

    [Fact]
    public void UriParser_GetStringValues_WhenNull_ReturnsEmpty()
    {
        var parser = new OpenIdUriParser();

        Assert.Equal(
            StringValues.Empty,
            parser.GetStringValues(Environment, AllowMissingDescriptor, null)
        );
    }

    [Fact]
    public void UriParser_GetStringValues_ReturnsTheUriAsWritten()
    {
        var parser = new OpenIdUriParser();
        var uri = new Uri("myapp://callback");

        Assert.Equal(
            "myapp://callback",
            parser.GetStringValues(Environment, AllowMissingDescriptor, uri)
        );
    }

    [Fact]
    public void UriParser_Parse_ReturnsUri()
    {
        var parser = new OpenIdUriParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, "https://example.com/path");

        Assert.Equal(new Uri("https://example.com/path"), result);
    }

    [Fact]
    public void UriParser_Parse_WhenMissingAndAllowed_ReturnsNull()
    {
        var parser = new OpenIdUriParser();

        Assert.Null(parser.Parse(Environment, AllowMissingDescriptor, StringValues.Empty));
    }

    [Fact]
    public void UriParser_Parse_WhenInvalid_Throws()
    {
        SetupErrorFactory();
        var parser = new OpenIdUriParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(Environment, AllowMissingDescriptor, "not a uri")
        );
    }

    [Fact]
    public void UriParser_Parse_WhenTooManyValues_Throws()
    {
        SetupErrorFactory();
        var parser = new OpenIdUriParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(
                Environment,
                AllowMissingDescriptor,
                new StringValues(["https://a.example", "https://b.example"])
            )
        );
    }

    #endregion

    #region TimeSpanParser Tests

    [Fact]
    public void TimeSpanParser_GetStringValues_ReturnsWholeSeconds()
    {
        var parser = new TimeSpanParser();

        Assert.Equal(
            "90",
            parser.GetStringValues(Environment, AllowMissingDescriptor, TimeSpan.FromSeconds(90))
        );
    }

    [Fact]
    public void TimeSpanParser_Parse_ReturnsTimeSpan()
    {
        var parser = new TimeSpanParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, "90");

        Assert.Equal(TimeSpan.FromSeconds(90), result);
    }

    [Fact]
    public void TimeSpanParser_Parse_WhenMissingAndAllowed_ReturnsZero()
    {
        var parser = new TimeSpanParser();

        Assert.Equal(
            TimeSpan.Zero,
            parser.Parse(Environment, AllowMissingDescriptor, StringValues.Empty)
        );
    }

    [Fact]
    public void TimeSpanParser_Parse_WhenInvalid_Throws()
    {
        SetupErrorFactory();
        var parser = new TimeSpanParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(Environment, AllowMissingDescriptor, "not-a-number")
        );
    }

    [Fact]
    public void TimeSpanParser_Parse_WhenTooManyValues_Throws()
    {
        SetupErrorFactory();
        var parser = new TimeSpanParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(Environment, AllowMissingDescriptor, new StringValues(["1", "2"]))
        );
    }

    #endregion
}
