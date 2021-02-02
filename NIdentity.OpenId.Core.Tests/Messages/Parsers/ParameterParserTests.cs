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
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
    public class ParameterParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;
        private readonly Mock<ITestParameterParser> _mockTestParameterParser;

        public ParameterParserTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
            _mockTestParameterParser = _mockRepository.Create<ITestParameterParser>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Separator_ThenValid()
        {
            var parser = new TestParameterParser(_mockTestParameterParser.Object, null);

            Assert.Equal(OpenIdConstants.ParameterSeparator, parser.Separator);
        }

        [Fact]
        public void StringComparison_ThenValid()
        {
            var parser = new TestParameterParser(_mockTestParameterParser.Object, null);

            Assert.Equal(StringComparison.Ordinal, parser.StringComparison);
        }

        [Fact]
        public void TryLoad_GivenValidValue_ThenLoadParsedValue()
        {
            var parser = new TestParameterParser(_mockTestParameterParser.Object, null);

            const string parameterName = "parameterName";
            var stringValues = new StringValues("value1");
            const string parsedValue = "value1";

            var context = _mockOpenIdMessageContext.Object;
            var descriptor = new ParameterDescriptor(parameterName);
            var parameter = new Parameter(descriptor);

            var parseResult = new ValidationResult<string>(parsedValue);
            _mockTestParameterParser
                .Setup(_ => _.TryParse(context, descriptor, stringValues, out parseResult))
                .Returns(true)
                .Verifiable();

            var success = parser.TryLoad(context, parameter, stringValues, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.ErrorDetails);
            Assert.Equal(stringValues, parameter.StringValues);
            Assert.Same(parsedValue, parameter.ParsedValue);
        }

        [Fact]
        public void TryLoad_GivenInvalidValue_ThenResetParsedValue()
        {
            var parser = new TestParameterParser(_mockTestParameterParser.Object, null);

            const string parameterName = "parameterName";
            var stringValues = new StringValues("value1");
            const string parsedValue = "value1";

            var context = _mockOpenIdMessageContext.Object;
            var descriptor = new ParameterDescriptor(parameterName);
            var parameter = new Parameter(descriptor);

            parameter.Load(stringValues, parsedValue);

            var parseResult = new ValidationResult<string>(new ErrorDetails { ErrorCode = "errorCode" });
            _mockTestParameterParser
                .Setup(_ => _.TryParse(context, descriptor, stringValues, out parseResult))
                .Returns(false)
                .Verifiable();

            var success = parser.TryLoad(context, parameter, stringValues, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.NotNull(result.ErrorDetails);
            Assert.Equal(stringValues, parameter.StringValues);
            Assert.Null(parameter.ParsedValue);
        }
    }
}
