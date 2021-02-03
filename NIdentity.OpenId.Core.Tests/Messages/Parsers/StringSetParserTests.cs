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

            var parsedValue = (IEnumerable<string>?)null;

            var stringValues = parser.Serialize(context, parsedValue);
            Assert.Equal(StringValues.Empty, stringValues);
        }

        [Fact]
        public void Serialize_GivenEmpty_ThenEmpty()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            var parsedValue = Enumerable.Empty<string>();

            var stringValues = parser.Serialize(context, parsedValue);
            Assert.Equal(StringValues.Empty, stringValues);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenOptional_ThenSuccess()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<IEnumerable<string>?>(
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
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenRequired_ThenError()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<IEnumerable<string>?>(
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
        public void TryParse_GivenMultipleValues_WhenDisallowMultipleValues_ThenError()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IEnumerable<string>?>(
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
        public void TryParse_GivenMultipleValues_WhenAllowMultipleValues_ThenSuccess()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IEnumerable<string>?>(
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
            Assert.NotNull(result.Value);
            Assert.Equal(stringValues, result.Value!);
        }

        [Fact]
        public void TryParse_GivenDuplicateValues_WhenAllowMultipleValues_ThenDistinctValues()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2", "value1", "value2" };

            var knownParameter = new KnownParameter<IEnumerable<string>?>(
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
            Assert.Equal(stringValues.Distinct(), result.Value);
        }

        [Fact]
        public void TryParse_GivenStringList_ThenSuccess()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "value1 value2";
            var expectedResult = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IEnumerable<string>?>(
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
            Assert.NotNull(result.Value);
            Assert.Equal(expectedResult, result.Value!);
        }

        [Fact]
        public void TryParse_GivenStringListWithDuplicates_ThenDistinctValues()
        {
            var parser = new StringSetParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "value1 value2 value1 value2";
            var expectedResult = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<IEnumerable<string>?>(
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
            Assert.NotNull(result.Value);
            Assert.Equal(expectedResult, result.Value!);
        }
    }
}
