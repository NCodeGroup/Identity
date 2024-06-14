#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Moq;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Servers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parameters;

public class ParameterTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Constructor_ThenValid()
    {
        var mockParser = MockRepository.Create<ParameterParser<string>>();

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value1" };

        var knownParameter = new KnownParameter<string>(parameterName, mockParser.Object)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter<string[]>
        {
            Descriptor = descriptor,
            StringValues = stringValues,
            ParsedValue = stringValues
        };

        Assert.Equal(descriptor, parameter.Descriptor);
        Assert.Equal(stringValues, parameter.StringValues);
        Assert.Same(stringValues, parameter.ParsedValue);
    }

    [Fact]
    public void Load_GivenEnumerable_ThenValid()
    {
        var mockParser = MockRepository.Create<ParameterParser<string>>();
        var mockOpenIdServer = MockRepository.Create<OpenIdServer>();

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<string>(parameterName, mockParser.Object)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var server = mockOpenIdServer.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var expectedParameter = new Parameter<string[]>
        {
            Descriptor = descriptor,
            StringValues = stringValues,
            ParsedValue = stringValues
        };

        mockParser
            .Setup(x => x.Load(server, descriptor, stringValues))
            .Returns(expectedParameter)
            .Verifiable();

        KnownParameter? knownParameterBase = knownParameter;
        mockOpenIdServer
            .Setup(x => x.KnownParameters.TryGet(parameterName, out knownParameterBase))
            .Returns(true)
            .Verifiable();

        var actualParameter = Parameter.Load(server, parameterName, stringValues.AsEnumerable());
        var typedParameter = Assert.IsType<Parameter<string[]>>(actualParameter);

        Assert.Equal(expectedParameter.Descriptor, typedParameter.Descriptor);
        Assert.Equal(expectedParameter.StringValues, typedParameter.StringValues);
        Assert.Same(expectedParameter.ParsedValue, typedParameter.ParsedValue);
    }
}
