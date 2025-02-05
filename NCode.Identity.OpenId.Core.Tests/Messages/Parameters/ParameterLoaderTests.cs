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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parameters;

public class ParameterLoaderTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose()
    {
        MockRepository.Verify();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_GivenNoParsedValue_ThenValid()
    {
        var loader = new ParameterLoader();

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        var mockOpenIdEnvironment = MockRepository.Create<OpenIdEnvironment>();
        var environment = mockOpenIdEnvironment.Object;

        var parameter = loader.Load(environment, descriptor, stringValues);

        Assert.Equal(descriptor, parameter.Descriptor);
        Assert.Equal(stringValues, parameter.StringValues);
    }

    // TODO
    // [Fact]
    // public void Load_GivenParsedValue_ThenValid()
    // {
    //     var loader = new ParameterLoader();
    //
    //     const string parameterName = "parameterName";
    //     var stringValues = new[] { "value1", "value2" };
    //
    //     var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);
    //     var mockOpenIdEnvironment = MockRepository.Create<OpenIdEnvironment>();
    //     var environment = mockOpenIdEnvironment.Object;
    //
    //     var parameter = loader.Load(environment, descriptor, stringValues, stringValues);
    //
    //     Assert.Equal(descriptor, parameter.Descriptor);
    //     Assert.Equal(stringValues, parameter.StringValues);
    //     Assert.Same(stringValues, parameter.ParsedValue);
    // }
}
