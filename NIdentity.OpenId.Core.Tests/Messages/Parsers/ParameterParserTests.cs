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
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
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
        public void Load_ThenValid()
        {
            var parser = new TestParameterParser(_mockTestParameterParser.Object, null);
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "stringValues";
            const string parsedValue = "parsedValue";

            var descriptor = new ParameterDescriptor(parameterName);
            var parameter = new Parameter(descriptor);

            _mockTestParameterParser
                .Setup(_ => _.Parse(context, descriptor, stringValues))
                .Returns(parsedValue)
                .Verifiable();

            parser.Load(context, parameter, stringValues);
            Assert.Equal(stringValues, parameter.StringValues);
            Assert.Same(parsedValue, parameter.ParsedValue);
        }
    }
}
