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

namespace NIdentity.OpenId.Core.Tests.Messages.Parameters
{
    public class ParameterTests : IDisposable
    {
        private readonly MockRepository _mockRepository;

        public ParameterTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Constructor_ThenValid()
        {
            var mockParser = _mockRepository.Create<ParameterParser<string>>();

            const string parameterName = "parameterName";

            var knownParameter = new KnownParameter<string>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                mockParser.Object);

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);

            Assert.Equal(descriptor, parameter.Descriptor);
            Assert.Equal(StringValues.Empty, parameter.StringValues);
            Assert.Null(parameter.ParsedValue);
        }

        [Fact]
        public void Load_ThenValid()
        {
            var mockParser = _mockRepository.Create<ParameterParser<string>>();

            const string parameterName = "parameterName";

            var knownParameter = new KnownParameter<string>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                mockParser.Object);

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);

            var stringValues = new[] { "value1", "value2" };
            var parsedValue = new object();

            parameter.Load(stringValues, parsedValue);

            Assert.Equal(stringValues, parameter.StringValues);
            Assert.Equal(parsedValue, parameter.ParsedValue);
        }

        [Fact]
        public void TryLoad_ThenValid()
        {
            var mockParser = _mockRepository.Create<ParameterParser<string>>();
            var mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
            var context = mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2" };

            var knownParameter = new KnownParameter<string>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                mockParser.Object);

            var descriptor = new ParameterDescriptor(knownParameter);
            var parameter = new Parameter(descriptor);

            var loadResult = ValidationResult.SuccessResult;
            mockParser
                .Setup(_ => _.TryLoad(context, parameter, stringValues, out loadResult))
                .Returns(true)
                .Verifiable();

            var success = parameter.TryLoad(context, stringValues, out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Empty(parameter.StringValues);
            Assert.Null(parameter.ParsedValue);
        }
    }
}
