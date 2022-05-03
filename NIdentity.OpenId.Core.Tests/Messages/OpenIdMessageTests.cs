using System;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages;

public class OpenIdMessageTests : IDisposable
{
    private readonly MockRepository _mockRepository;
    private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

    public OpenIdMessageTests()
    {
        _mockRepository = new MockRepository(MockBehavior.Strict);
        _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        _mockRepository.Verify();
    }

    [Fact]
    public void Initialize_WhenNotInitialized_ThenValid()
    {
        var context = _mockOpenIdMessageContext.Object;
        var message = new TestOpenIdMessage();

        message.Initialize(context, Array.Empty<Parameter>());

        Assert.Empty(message.Parameters);
        Assert.Same(context, message.Context);
    }

    [Fact]
    public void Initialize_WhenAlreadyInitialized_ThenThrows()
    {
        var context = _mockOpenIdMessageContext.Object;
        var message = new TestOpenIdMessage();

        message.Initialize(context, Array.Empty<Parameter>());

        Assert.Throws<InvalidOperationException>(() =>
        {
            message.Initialize(context, Array.Empty<Parameter>());
        });
    }

    [Fact]
    public void DefaultConstructor_WhenNoContext_ThenThrows()
    {
        var message = new TestOpenIdMessage();

        Assert.Throws<InvalidOperationException>(() => message.Context);
    }

    [Fact]
    public void DefaultConstructor_WhenNoParameters_ThenThrows()
    {
        var message = new TestOpenIdMessage();

        Assert.Throws<InvalidOperationException>(() => message.Parameters);
    }

    [Fact]
    public void TryGetValue_WhenNotFound_ThenReturnsEmpty()
    {
        var context = _mockOpenIdMessageContext.Object;
        var message = new TestOpenIdMessage();
        message.Initialize(context, Array.Empty<Parameter>());

        var success = message.TryGetValue("non-existent-key", out var stringValues);
        Assert.False(success);
        Assert.Equal(StringValues.Empty, stringValues);
    }

    [Fact]
    public void TryGetValue_WhenFound_ThenValid()
    {
        var message = new TestOpenIdMessage();

        const string parameterName = "parameterName";
        var expectedValue = new[] { "value1", "value2" };

        var context = _mockOpenIdMessageContext.Object;
        var parameter = new Parameter(new ParameterDescriptor(parameterName), expectedValue);
        message.Initialize(context, new[] { parameter });

        var getSuccess = message.TryGetValue(parameterName, out var actualValue);
        Assert.True(getSuccess);
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void GetKnownParameter_WhenNotFound_ThenValid()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();
        var context = _mockOpenIdMessageContext.Object;
        message.Initialize(context, Array.Empty<Parameter>());

        var result = message.GetKnownParameter(knownParameter);
        Assert.Null(result);
    }

    [Fact]
    public void GetKnownParameter_WhenParsedValueIsSet_ThenReturnParsedValue()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var stringValues = new StringValues("invalid_json");
        var parsedValue = new TestNestedObject();

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();
        var context = _mockOpenIdMessageContext.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter(descriptor, stringValues, parsedValue);
        message.Initialize(context, new[] { parameter });

        var result = message.GetKnownParameter(knownParameter);
        Assert.Same(parsedValue, result);
    }

    [Fact]
    public void GetKnownParameter_WhenParsedValueIsNotSet_ThenReturnDefault()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };
        var stringValues = JsonSerializer.Serialize(parsedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();
        var context = _mockOpenIdMessageContext.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter(descriptor, stringValues);
        message.Initialize(context, new[] { parameter });

        var result = message.GetKnownParameter(knownParameter);
        Assert.Null(result);
        Assert.Null(parameter.ParsedValue);
    }

    [Fact]
    public void SetKnownParameter_WhenContextIsNull_ThenThrows()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();

        Assert.Throws<InvalidOperationException>(() =>
        {
            message.SetKnownParameter(knownParameter, parsedValue);
        });
    }

    [Fact]
    public void SetKnownParameter_GivenNullParsedValue_ThenRemovesParameter()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };
        var stringValues = JsonSerializer.Serialize(parsedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();
        var context = _mockOpenIdMessageContext.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter(descriptor, stringValues, parsedValue);
        message.Initialize(context, new[] { parameter });

        message.SetKnownParameter(knownParameter, null);

        Assert.Empty(message.Parameters);
    }

    [Fact]
    public void SetKnownParameter_WhenNullParserResult_ThenRemovesParameter()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };
        var stringValues = JsonSerializer.Serialize(parsedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();
        var context = _mockOpenIdMessageContext.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter(descriptor, stringValues);
        message.Initialize(context, new[] { parameter });

        mockParameterParser
            .Setup(_ => _.Serialize(context, parsedValue))
            .Returns(StringValues.Empty)
            .Verifiable();

        message.SetKnownParameter(knownParameter, parsedValue);

        Assert.Empty(message.Parameters);
    }

    [Fact]
    public void SetKnownParameter_WhenEmpty_ThenParameterAdded()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };
        var stringValues = JsonSerializer.Serialize(parsedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();
        var context = _mockOpenIdMessageContext.Object;
        message.Initialize(context, Array.Empty<Parameter>());

        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter(descriptor, stringValues, parsedValue);

        mockParameterParser
            .Setup(_ => _.Serialize(context, parsedValue))
            .Returns(stringValues)
            .Verifiable();

        mockParameterParser
            .Setup(_ => _.Load(context, descriptor, stringValues, parsedValue))
            .Returns(parameter)
            .Verifiable();

        message.SetKnownParameter(knownParameter, parsedValue);

        var (key, value) = Assert.Single(message.Parameters);
        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, value.Descriptor.ParameterName);
        Assert.Equal(stringValues, value.StringValues);
        Assert.Equal(parsedValue, value.ParsedValue);
    }

    [Fact]
    public void SetKnownParameter_WhenExisting_ThenParameterReplaced()
    {
        var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue1"
        };
        var stringValues = JsonSerializer.Serialize(parsedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            mockParameterParser.Object);

        var message = new TestOpenIdMessage();
        var context = _mockOpenIdMessageContext.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter(descriptor, stringValues, parsedValue);
        message.Initialize(context, new[] { parameter });

        parsedValue.NestedPropertyName1 = "NestedPropertyValue2";
        stringValues = JsonSerializer.Serialize(parsedValue);
        parameter = new Parameter(descriptor, stringValues, parsedValue);

        mockParameterParser
            .Setup(_ => _.Serialize(context, parsedValue))
            .Returns(stringValues)
            .Verifiable();

        mockParameterParser
            .Setup(_ => _.Load(context, descriptor, stringValues, parsedValue))
            .Returns(parameter)
            .Verifiable();

        message.SetKnownParameter(knownParameter, parsedValue);

        var (key, value) = Assert.Single(message.Parameters);
        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, value.Descriptor.ParameterName);
        Assert.Equal(stringValues, value.StringValues);
        Assert.Equal(parsedValue, value.ParsedValue);
    }
}