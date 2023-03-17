using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Results;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class DisplayTypeParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdMessageContext> MockOpenIdMessageContext { get; }

    public DisplayTypeParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdMessageContext = MockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenPage_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdMessageContext.Object, DisplayType.Page);
        Assert.Equal("page", result);
    }

    [Fact]
    public void Serialize_GivenPopup_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdMessageContext.Object, DisplayType.Popup);
        Assert.Equal("popup", result);
    }

    [Fact]
    public void Serialize_GivenTouch_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdMessageContext.Object, DisplayType.Touch);
        Assert.Equal("touch", result);
    }

    [Fact]
    public void Serialize_GivenWap_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdMessageContext.Object, DisplayType.Wap);
        Assert.Equal("wap", result);
    }

    [Fact]
    public void Serialize_GivenUnknown_ThenEmpty()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdMessageContext.Object, DisplayType.Unspecified);
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<DisplayType?>(
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
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<DisplayType?>(
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
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "page", "wap" };

        var knownParameter = new KnownParameter<DisplayType?>(
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
    public void Parse_GivenPageWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "page";

        var knownParameter = new KnownParameter<DisplayType?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Page, result);
    }

    [Fact]
    public void Parse_GivenPageWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "PAGE";

        var knownParameter = new KnownParameter<DisplayType?>(
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
    public void Parse_GivenPopupWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "popup";

        var knownParameter = new KnownParameter<DisplayType?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Popup, result);
    }

    [Fact]
    public void Parse_GivenPopupWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "POPUP";

        var knownParameter = new KnownParameter<DisplayType?>(
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
    public void Parse_GivenTouchWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "touch";

        var knownParameter = new KnownParameter<DisplayType?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Touch, result);
    }

    [Fact]
    public void Parse_GivenTouchWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "TOUCH";

        var knownParameter = new KnownParameter<DisplayType?>(
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
    public void Parse_GivenWapWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "wap";

        var knownParameter = new KnownParameter<DisplayType?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Wap, result);
    }

    [Fact]
    public void Parse_GivenWapWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "WAP";

        var knownParameter = new KnownParameter<DisplayType?>(
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
        var parser = new DisplayTypeParser();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "invalid_value";

        var knownParameter = new KnownParameter<DisplayType?>(
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
