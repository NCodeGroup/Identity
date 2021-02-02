#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
    public class ResponseTypeParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public ResponseTypeParserTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Serialize_GivenCode_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseTypes.Code);
            Assert.Equal("code", result);
        }

        [Fact]
        public void Serialize_GivenIdToken_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseTypes.IdToken);
            Assert.Equal("id_token", result);
        }

        [Fact]
        public void Serialize_GivenToken_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseTypes.Token);
            Assert.Equal("token", result);
        }

        [Fact]
        public void Serialize_GivenUnknown_ThenEmpty()
        {
            var parser = new ResponseTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseTypes.Unknown);
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenOptional_ThenSuccess()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: true,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                _mockOpenIdMessageContext.Object,
                descriptor,
                StringValues.Empty,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenRequired_ThenError()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                _mockOpenIdMessageContext.Object,
                descriptor,
                StringValues.Empty,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenMultipleValues_ThenSuccess()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "code id_token token";
            const ResponseTypes expectedValue = ResponseTypes.Code | ResponseTypes.IdToken | ResponseTypes.Token;

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.ErrorDetails);
            Assert.Equal(expectedValue, result.Value);
        }

        [Fact]
        public void TryParse_GivenCodeWithValidCase_ThenSuccess()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "code";

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(ResponseTypes.Code, result.Value);
        }

        [Fact]
        public void TryParse_GivenCodeWithInvalidCase_ThenError()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "CODE";

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenIdTokenWithValidCase_ThenSuccess()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "id_token";

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(ResponseTypes.IdToken, result.Value);
        }

        [Fact]
        public void TryParse_GivenIdTokenWithInvalidCase_ThenError()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "ID_TOKEN";

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenTokenWithValidCase_ThenSuccess()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "token";

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(ResponseTypes.Token, result.Value);
        }

        [Fact]
        public void TryParse_GivenTokenWithInvalidCase_ThenError()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "TOKEN";

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenInvalidValue_ThenError()
        {
            var parser = new ResponseTypeParser();

            var knownParameter = new KnownParameter<ResponseTypes?>(
                "parameterName",
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "invalid_value";

            var success = parser.TryParse(_mockOpenIdMessageContext.Object, descriptor, stringValue, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.Null(result.Value);
        }
    }
}
