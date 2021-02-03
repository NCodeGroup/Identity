﻿#region Copyright Preamble

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
    public class KnownParametersTests : IDisposable
    {
        private readonly MockRepository _mockRepository;

        public KnownParametersTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Register_GivenKnownParameter_ThenValid()
        {
            // known parameters must have unique names
            var parameterName = Guid.NewGuid().ToString("N");

            const bool optional = false;
            const bool allowMultipleValues = false;

            var mockParser = _mockRepository.Create<ParameterParser<string>>();

            var knownParameter = new KnownParameter<string>(
                parameterName,
                optional,
                allowMultipleValues,
                mockParser.Object);

            var registerResult = KnownParameters.Register(knownParameter);
            Assert.Same(knownParameter, registerResult);

            var success = KnownParameters.TryGet(parameterName, out var tryGetResult);
            Assert.True(success);
            Assert.Same(knownParameter, tryGetResult);
        }

        [Fact]
        public void TryGet_GivenUnknownParameter_ThenValid()
        {
            const string parameterName = "parameterName";

            var success = KnownParameters.TryGet(parameterName, out var result);
            Assert.False(success);
            Assert.Null(result);
        }
    }
}
