using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class CodeChallengeMethodParserTests : IDisposable
{
    private readonly MockRepository _mockRepository;
    private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

    public CodeChallengeMethodParserTests()
    {
        _mockRepository = new MockRepository(MockBehavior.Strict);
        _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        _mockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenPlain_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var result = parser.Serialize(_mockOpenIdMessageContext.Object, CodeChallengeMethod.Plain);
        Assert.Equal("plain", result);
    }

    [Fact]
    public void Serialize_GivenS256_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var result = parser.Serialize(_mockOpenIdMessageContext.Object, CodeChallengeMethod.Sha256);
        Assert.Equal("S256", result);
    }

    [Fact]
    public void Serialize_GivenUnknown_ThenEmpty()
    {
        var parser = new CodeChallengeMethodParser();
        var result = parser.Serialize(_mockOpenIdMessageContext.Object, CodeChallengeMethod.Unspecified);
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
        {
            parser.Parse(context, descriptor, stringValues);
        });
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
        {
            parser.Parse(context, descriptor, stringValues);
        });
    }

    [Fact]
    public void Parse_GivenPlainWithValidCase_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "plain";

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(CodeChallengeMethod.Plain, result);
    }

    [Fact]
    public void Parse_GivenPlainWithInvalidCase_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "PLAIN";

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
        {
            parser.Parse(context, descriptor, stringValues);
        });
    }

    [Fact]
    public void Parse_GivenS256WithValidCase_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "S256";

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(CodeChallengeMethod.Sha256, result);
    }

    [Fact]
    public void Parse_GivenS256WithInvalidCase_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "s256";

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
        {
            parser.Parse(context, descriptor, stringValues);
        });
    }

    [Fact]
    public void Parse_GivenInvalidValue_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "invalid_value";

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
        {
            parser.Parse(context, descriptor, stringValues);
        });
    }
}