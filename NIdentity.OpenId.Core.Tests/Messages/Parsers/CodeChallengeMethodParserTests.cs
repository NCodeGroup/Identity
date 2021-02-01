using System;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
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
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, CodeChallengeMethod.S256);
            Assert.Equal("S256", result);
        }

        [Fact]
        public void Serialize_GivenUnknown_ThenEmpty()
        {
            var parser = new CodeChallengeMethodParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, CodeChallengeMethod.Unknown);
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenOptional_ThenSuccess()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: true, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, StringValues.Empty, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenRequired_ThenError()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: false, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, StringValues.Empty, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenMultipleValues_ThenError()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: false, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            var stringValues = new[] { "value1", "value2" };
            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValues, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenPlainWithValidCase_ThenSuccess()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: false, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValues = "plain";
            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValues, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(CodeChallengeMethod.Plain, result.Value);
        }

        [Fact]
        public void TryParse_GivenPlainWithInvalidCase_ThenError()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: false, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValues = "PLAIN";
            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValues, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenS256WithValidCase_ThenSuccess()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: false, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValues = "S256";
            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValues, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(CodeChallengeMethod.S256, result.Value);
        }

        [Fact]
        public void TryParse_GivenS256WithInvalidCase_ThenError()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: false, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValues = "s256";
            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValues, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenInvalidValue_ThenError()
        {
            var parser = new CodeChallengeMethodParser();

            var knownParameter = new KnownParameter<string?>("parameterName", optional: false, allowMultipleValues: false, ParameterParsers.String);
            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValues = "invalid_value";
            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValues, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }
    }
}
