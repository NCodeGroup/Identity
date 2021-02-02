using System;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages
{
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
        public void DefaultConstructor_ThenValid()
        {
            var message = new TestOpenIdMessage();

            Assert.Empty(message.Parameters);
            Assert.Null(message.Context);
        }

        [Fact]
        public void Context_WhenSet_ThenValid()
        {
            var message = new TestOpenIdMessage
            {
                Context = _mockOpenIdMessageContext.Object
            };

            Assert.Same(_mockOpenIdMessageContext.Object, message.Context);
        }

        [Fact]
        public void TryGetValue_WhenNotFound_ThenReturnsEmpty()
        {
            var message = new TestOpenIdMessage();

            var success = message.TryGetValue("non-existent-key", out var stringValues);
            Assert.False(success);
            Assert.Equal(StringValues.Empty, stringValues);
        }

        [Fact]
        public void TryGetValue_WhenFound_ThenValid()
        {
            var message = new TestOpenIdMessage { Context = _mockOpenIdMessageContext.Object };

            const string parameterName = "parameterName";
            var expectedValue = new[] { "value1", "value2" };

            var loadSuccess = message.TryLoad(parameterName, expectedValue, out var loadResult);
            Assert.True(loadSuccess);
            Assert.False(loadResult.HasError);

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

            var message = new TestOpenIdMessage
            {
                Context = _mockOpenIdMessageContext.Object
            };

            var result = message.GetKnownParameter(knownParameter);
            Assert.Null(result);
        }

        [Fact]
        public void GetKnownParameter_WhenParsedValueIsSet_ThenReturnParsedValue()
        {
            var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

            var message = new TestOpenIdMessage
            {
                Context = _mockOpenIdMessageContext.Object
            };

            const string parameterName = "parameterName";
            var stringValues = new StringValues("invalid_json");
            var parsedValue = new TestNestedObject();

            var knownParameter = new KnownParameter<TestNestedObject?>(
                parameterName,
                optional: true,
                allowMultipleValues: false,
                mockParameterParser.Object);

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);
            parameter.Load(stringValues, parsedValue);

            message.Parameters[knownParameter.Name] = parameter;

            var result = message.GetKnownParameter(knownParameter);
            Assert.Same(parsedValue, result);
        }

        [Fact]
        public void GetKnownParameter_WhenParsedValueIsNotSet_ThenReturnDefault()
        {
            var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

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

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);
            parameter.Load(stringValues, null);

            message.Parameters[knownParameter.Name] = parameter;

            var result = message.GetKnownParameter(knownParameter);
            Assert.Null(result);
            Assert.Null(parameter.ParsedValue);
        }

        [Fact]
        public void SetKnownParameter_WhenContextIsNull_ThenThrows()
        {
            var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

            var message = new TestOpenIdMessage();

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

            Assert.Throws<InvalidOperationException>(() => { message.SetKnownParameter(knownParameter, parsedValue); });
        }

        [Fact]
        public void SetKnownParameter_GivenNullParsedValue_ThenRemovesParameter()
        {
            var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

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

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);
            parameter.Load(stringValues, parsedValue);

            message.Parameters[parameterName] = parameter;

            message.SetKnownParameter(knownParameter, null);

            Assert.Empty(message.Parameters);
        }

        [Fact]
        public void SetKnownParameter_WhenNullParserResult_ThenRemovesParameter()
        {
            var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

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

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);
            parameter.Load(stringValues, null);

            message.Parameters[parameterName] = parameter;

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

            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

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

            mockParameterParser
                .Setup(_ => _.Serialize(context, parsedValue))
                .Returns(stringValues)
                .Verifiable();

            message.SetKnownParameter(knownParameter, parsedValue);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(parameterName, key);
            Assert.Equal(parameterName, value.Descriptor.ParameterName);
            Assert.Equal(stringValues, value.StringValues);
            Assert.Equal(parsedValue, value.ParsedValue);
        }

        [Fact]
        public void SetKnownParameter_WhenExisting_ThenParameterUpdated()
        {
            var mockParameterParser = _mockRepository.Create<ParameterParser<TestNestedObject?>>();

            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

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

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);
            parameter.Load(stringValues, parsedValue);

            message.Parameters[parameterName] = parameter;

            parsedValue.NestedPropertyName1 = "NestedPropertyValue2";
            stringValues = JsonSerializer.Serialize(parsedValue);

            mockParameterParser
                .Setup(_ => _.Serialize(context, parsedValue))
                .Returns(stringValues)
                .Verifiable();

            message.SetKnownParameter(knownParameter, parsedValue);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(parameterName, key);
            Assert.Equal(parameterName, value.Descriptor.ParameterName);
            Assert.Equal(stringValues, value.StringValues);
            Assert.Equal(parsedValue, value.ParsedValue);
        }

        [Fact]
        public void TryLoad_WhenContextIsNull_ThenThrows()
        {
            var message = new TestOpenIdMessage();

            const string parameterName = "parameterName";
            var parsedValue = new TestNestedObject
            {
                NestedPropertyName1 = "NestedPropertyValue"
            };
            var stringValues = JsonSerializer.Serialize(parsedValue);

            Assert.Throws<InvalidOperationException>(() => { message.TryLoad(parameterName, stringValues, out _); });
        }

        [Fact]
        public void TryLoad_GivenMissingUnknownParameter_ThenParameterAdded()
        {
            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

            const string parameterName = "parameterName";
            var parsedValue = new TestNestedObject
            {
                NestedPropertyName1 = "NestedPropertyValue"
            };
            var stringValues = JsonSerializer.Serialize(parsedValue);

            var success = message.TryLoad(parameterName, stringValues, out var result);
            Assert.True(success);
            Assert.False(result.HasError);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(parameterName, key);
            Assert.Equal(parameterName, value.Descriptor.ParameterName);
            Assert.Equal(stringValues, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void TryLoad_GivenExistingUnknownParameter_ThenParameterAdded()
        {
            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

            const string parameterName = "parameterName";
            var parsedValue = new TestNestedObject
            {
                NestedPropertyName1 = "NestedPropertyValue1"
            };
            var stringValues = JsonSerializer.Serialize(parsedValue);

            var descriptor = new ParameterDescriptor(parameterName);
            var parameter = new Parameter(descriptor);
            parameter.Load(stringValues, parsedValue);

            message.Parameters[parameterName] = parameter;

            parsedValue.NestedPropertyName1 = "NestedPropertyValue2";
            stringValues = JsonSerializer.Serialize(parsedValue);

            var success = message.TryLoad(parameterName, stringValues, out var result);
            Assert.True(success);
            Assert.False(result.HasError);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(parameterName, key);
            Assert.Equal(parameterName, value.Descriptor.ParameterName);
            Assert.Equal(stringValues, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void TryLoad_GivenExistingUnknownParameter_WhenLoadFails_ThenParameterNotUpdated()
        {
            var context = _mockOpenIdMessageContext.Object;
            var message = new TestOpenIdMessage
            {
                Context = context
            };

            const string parameterName = "parameterName";
            var parsedValue = new TestNestedObject
            {
                NestedPropertyName1 = "NestedPropertyValue1"
            };
            var stringValues = JsonSerializer.Serialize(parsedValue);

            var descriptor = new ParameterDescriptor(parameterName);
            var mockParameter = _mockRepository.Create<Parameter>(descriptor);

            var loadResult = new ValidationResult(new ErrorDetails { ErrorCode = "errorCode" });
            mockParameter
                .Setup(_ => _.TryLoad(context, stringValues, out loadResult))
                .Returns(false)
                .Verifiable();
            mockParameter
                .Setup(_ => _.Load(stringValues, parsedValue))
                .CallBase()
                .Verifiable();

            mockParameter.Object.Load(stringValues, parsedValue);
            message.Parameters[parameterName] = mockParameter.Object;

            var newParsedValue = new TestNestedObject
            {
                NestedPropertyName1 = "NestedPropertyValue1"
            };
            var newStringValues = JsonSerializer.Serialize(newParsedValue);

            var success = message.TryLoad(parameterName, newStringValues, out var result);
            Assert.False(success);
            Assert.Equal(loadResult, result);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(parameterName, key);
            Assert.Equal(parameterName, value.Descriptor.ParameterName);
            Assert.Equal(stringValues, value.StringValues);
            Assert.Same(parsedValue, value.ParsedValue);
        }
    }
}
