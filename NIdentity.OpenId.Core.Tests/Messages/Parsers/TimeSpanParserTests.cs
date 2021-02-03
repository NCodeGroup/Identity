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
    public class TimeSpanParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public TimeSpanParserTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Serialize_GivenNull_ThenEmpty()
        {
            var parser = new TimeSpanParser();
            var context = _mockOpenIdMessageContext.Object;

            var stringValue = parser.Serialize(context, null);
            Assert.Equal(StringValues.Empty, stringValue);
        }

        [Fact]
        public void Serialize_GivenValue_ThenValid()
        {
            var parser = new TimeSpanParser();
            var context = _mockOpenIdMessageContext.Object;

            var timeSpan = TimeSpan.FromSeconds(123.456);

            var stringValue = parser.Serialize(context, timeSpan);
            Assert.Equal("123", stringValue);
        }

        [Fact]
        public void TryParse_GivenNull_WhenOptional_ThenSuccess()
        {
            var parser = new TimeSpanParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<TimeSpan?>(
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
        public void TryParse_GivenNull_WhenRequired_ThenError()
        {
            var parser = new TimeSpanParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<TimeSpan?>(
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
        public void TryParse_GivenMultipleValues_DisallowMultipleValues_ThenError()
        {
            var parser = new TimeSpanParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "123", "456" };

            var knownParameter = new KnownParameter<TimeSpan?>(
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
        public void TryParse_GivenMultipleValues_AllowMultipleValues_ThenSuccess()
        {
            var parser = new TimeSpanParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "123", "456" };
            var expectedValue = TimeSpan.FromSeconds(123 + 456);

            var knownParameter = new KnownParameter<TimeSpan?>(
                parameterName,
                optional: false,
                allowMultipleValues: true,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(expectedValue, result.Value);
        }

        [Fact]
        public void TryParse_GivenSingleValue_ThenSuccess()
        {
            var parser = new TimeSpanParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "123" };
            var expectedValue = TimeSpan.FromSeconds(123);

            var knownParameter = new KnownParameter<TimeSpan?>(
                parameterName,
                optional: false,
                allowMultipleValues: true,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(expectedValue, result.Value);
        }
    }
}
