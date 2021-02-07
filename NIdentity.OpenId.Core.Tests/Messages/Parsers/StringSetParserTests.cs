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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
    public class StringSetParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public StringSetParserTests()
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
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            var parsedValue = (IReadOnlyCollection<string>?)null;

            var stringValues = parser.Serialize(context, parsedValue);
            Assert.Equal(StringValues.Empty, stringValues);
        }

        [Fact]
        public void Serialize_GivenEmpty_ThenEmpty()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            var parsedValue = Array.Empty<string>();

            var stringValues = parser.Serialize(context, parsedValue);
            Assert.Equal(StringValues.Empty, stringValues);
        }

        [Fact]
        public void Parse_GivenEmpty_WhenOptional_ThenValid()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
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
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
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
        public void Parse_GivenMultipleValues_WhenDisallowMultipleValues_ThenThrows()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
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
        public void Parse_GivenMultipleValues_WhenAllowMultipleValues_ThenValid()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
                parameterName,
                optional: false,
                allowMultipleValues: true,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(stringValues, result!);
        }

        [Fact]
        public void Parse_GivenDuplicateValues_WhenAllowMultipleValues_ThenDistinctValues()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2", "value1", "value2" };

            var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
                parameterName,
                optional: false,
                allowMultipleValues: true,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(stringValues.Distinct(), result);
        }

        [Fact]
        public void Parse_GivenStringList_ThenValid()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "value1 value2";
            var expectedResult = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(expectedResult, result!);
        }

        [Fact]
        public void Parse_GivenStringListWithDuplicates_ThenDistinctValues()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "value1 value2 value1 value2";
            var expectedResult = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var result = parser.Parse(context, descriptor, stringValues);
            Assert.Equal(expectedResult, result!);
        }
    }
}
