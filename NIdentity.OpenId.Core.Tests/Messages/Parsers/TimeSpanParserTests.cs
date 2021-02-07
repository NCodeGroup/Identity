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
        public void Parse_GivenNull_WhenOptional_ThenValid()
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

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Null(result);
        }

        [Fact]
        public void Parse_GivenNull_WhenRequired_ThenThrows()
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

            Assert.Throws<OpenIdException>(() =>
            {
                parser.Parse(context, descriptor, stringValues);
            });
        }

        [Fact]
        public void Parse_GivenMultipleValues_DisallowMultipleValues_ThenThrows()
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

            Assert.Throws<OpenIdException>(() =>
            {
                parser.Parse(context, descriptor, stringValues);
            });
        }

        [Fact]
        public void Parse_GivenMultipleValues_AllowMultipleValues_ThenValid()
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

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public void Parse_GivenSingleValue_ThenValid()
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

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(expectedValue, result);
        }
    }
}
