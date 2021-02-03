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
    public class ResponseModeParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public ResponseModeParserTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Serialize_GivenQuery_ThenValid()
        {
            var parser = new ResponseModeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseMode.Query);
            Assert.Equal("query", result);
        }

        [Fact]
        public void Serialize_GivenFragment_ThenValid()
        {
            var parser = new ResponseModeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseMode.Fragment);
            Assert.Equal("fragment", result);
        }

        [Fact]
        public void Serialize_GivenFormPost_ThenValid()
        {
            var parser = new ResponseModeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseMode.FormPost);
            Assert.Equal("form_post", result);
        }

        [Fact]
        public void Serialize_GivenUnknown_ThenEmpty()
        {
            var parser = new ResponseModeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, ResponseMode.Unknown);
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenOptional_ThenSuccess()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: true,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenRequired_ThenError()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenMultipleValues_ThenError()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenQueryWithValidCase_ThenSuccess()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "query";

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(ResponseMode.Query, result.Value);
        }

        [Fact]
        public void TryParse_GivenQueryWithInvalidCase_ThenError()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "QUERY";

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenFragmentWithValidCase_ThenSuccess()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "fragment";

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(ResponseMode.Fragment, result.Value);
        }

        [Fact]
        public void TryParse_GivenFragmentWithInvalidCase_ThenError()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "FRAGMENT";

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenFormPostWithValidCase_ThenSuccess()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "form_post";

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(ResponseMode.FormPost, result.Value);
        }

        [Fact]
        public void TryParse_GivenFormPostWithInvalidCase_ThenError()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "FORM_POST";

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenInvalidValue_ThenError()
        {
            var parser = new ResponseModeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "invalid_value";

            var knownParameter = new KnownParameter<ResponseMode?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }
    }
}
