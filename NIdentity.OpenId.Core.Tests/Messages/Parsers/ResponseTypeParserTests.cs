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
using NIdentity.OpenId.Validation;
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
        public void Serialize_GivenNone_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseTypes.None);
            Assert.Equal("none", result);
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
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseTypes.Unspecified);
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void Parse_GivenEmpty_WhenOptional_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<ResponseTypes?>(
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
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<ResponseTypes?>(
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
        public void Parse_GivenMultipleValues_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "code id_token token";
            const ResponseTypes expectedResult = ResponseTypes.Code | ResponseTypes.IdToken | ResponseTypes.Token;

            var knownParameter = new KnownParameter<ResponseTypes?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void Parse_GivenNoneWithValidCase_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "none";

            var knownParameter = new KnownParameter<ResponseTypes?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(ResponseTypes.None, result);
        }

        [Fact]
        public void Parse_GivenNoneWithInvalidCase_ThenThrows()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "NONE";

            var knownParameter = new KnownParameter<ResponseTypes?>(
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
        public void Parse_GivenCodeWithValidCase_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "code";

            var knownParameter = new KnownParameter<ResponseTypes?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(ResponseTypes.Code, result);
        }

        [Fact]
        public void Parse_GivenCodeWithInvalidCase_ThenThrows()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "CODE";

            var knownParameter = new KnownParameter<ResponseTypes?>(
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
        public void Parse_GivenIdTokenWithValidCase_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "id_token";

            var knownParameter = new KnownParameter<ResponseTypes?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(ResponseTypes.IdToken, result);
        }

        [Fact]
        public void Parse_GivenIdTokenWithInvalidCase_ThenThrows()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "ID_TOKEN";

            var knownParameter = new KnownParameter<ResponseTypes?>(
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
        public void Parse_GivenTokenWithValidCase_ThenValid()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "token";

            var knownParameter = new KnownParameter<ResponseTypes?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(ResponseTypes.Token, result);
        }

        [Fact]
        public void Parse_GivenTokenWithInvalidCase_ThenThrows()
        {
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "TOKEN";

            var knownParameter = new KnownParameter<ResponseTypes?>(
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
            var parser = new ResponseTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "invalid_value";

            var knownParameter = new KnownParameter<ResponseTypes?>(
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
}
