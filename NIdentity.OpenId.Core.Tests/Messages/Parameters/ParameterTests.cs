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
using System.Linq;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parameters;

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
        var stringValues = new[] { "value1", "value1" };

        var knownParameter = new KnownParameter<string>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            mockParser.Object);

        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter(descriptor, stringValues, stringValues);

        Assert.Equal(descriptor, parameter.Descriptor);
        Assert.Equal(stringValues, parameter.StringValues);
        Assert.Same(stringValues, parameter.ParsedValue);
    }

    [Fact]
    public void Load_GivenEnumerable_ThenValid()
    {
        var mockParser = _mockRepository.Create<ParameterParser<string>>();
        var mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<string>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            mockParser.Object);

        var context = mockOpenIdMessageContext.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var expectedParameter = new Parameter(descriptor, stringValues, stringValues);

        mockParser
            .Setup(_ => _.Load(context, descriptor, stringValues))
            .Returns(expectedParameter)
            .Verifiable();

        KnownParameter? knownParameterBase = knownParameter;
        mockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameterBase))
            .Returns(true)
            .Verifiable();

        var actualParameter = Parameter.Load(context, parameterName, stringValues.AsEnumerable());

        Assert.Equal(expectedParameter.Descriptor, actualParameter.Descriptor);
        Assert.Equal(expectedParameter.StringValues, actualParameter.StringValues);
        Assert.Same(expectedParameter.ParsedValue, actualParameter.ParsedValue);
    }
}