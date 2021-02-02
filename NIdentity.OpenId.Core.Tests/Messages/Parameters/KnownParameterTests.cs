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
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parameters
{
    public class KnownParameterTests : IDisposable
    {
        private readonly MockRepository _mockRepository;

        public KnownParameterTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Constructor_GivenValid_ThenValid()
        {
            const string parameterName = "parameterName";
            const bool optional = false;
            const bool allowMultipleValues = false;

            var mockParser = _mockRepository.Create<ParameterParser<string>>();

            var parameter = new KnownParameter<string>(parameterName, optional, allowMultipleValues, mockParser.Object);

            Assert.Equal(parameterName, parameter.Name);
            Assert.Equal(typeof(string), parameter.ValueType);
            Assert.Equal(optional, parameter.Optional);
            Assert.Equal(allowMultipleValues, parameter.AllowMultipleValues);
            Assert.Same(mockParser.Object, parameter.Loader);
            Assert.Same(mockParser.Object, parameter.Parser);
        }

        [Fact]
        public void Constructor_GivenNullName_ThenThrow()
        {
            const bool optional = false;
            const bool allowMultipleValues = false;

            var mockParser = _mockRepository.Create<ParameterParser<string>>();

            Assert.Throws<ArgumentNullException>(() =>
            {
                var _ = new KnownParameter<string>(
                    null!,
                    optional,
                    allowMultipleValues,
                    mockParser.Object);
            });
        }

        [Fact]
        public void Constructor_GivenEmptyName_ThenThrow()
        {
            const bool optional = false;
            const bool allowMultipleValues = false;

            var mockParser = _mockRepository.Create<ParameterParser<string>>();

            Assert.Throws<ArgumentNullException>(() =>
            {
                var _ = new KnownParameter<string>(
                    string.Empty,
                    optional,
                    allowMultipleValues,
                    mockParser.Object);
            });
        }
    }
}
