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

using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parameters;

public class ParameterLoaderTests : IDisposable
{
    private MockRepository MockRepository { get; }

    public ParameterLoaderTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Load_GivenNoParsedValue_ThenValid()
    {
        var loader = new ParameterLoader();

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var descriptor = new ParameterDescriptor(parameterName);
        var mockOpenIdContext = MockRepository.Create<IOpenIdContext>();
        var context = mockOpenIdContext.Object;

        var parameter = loader.Load(context, descriptor, stringValues);

        Assert.Equal(descriptor, parameter.Descriptor);
        Assert.Equal(stringValues, parameter.StringValues);
    }

    [Fact]
    public void Load_GivenParsedValue_ThenValid()
    {
        var loader = new ParameterLoader();

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var descriptor = new ParameterDescriptor(parameterName);
        var mockOpenIdContext = MockRepository.Create<IOpenIdContext>();
        var context = mockOpenIdContext.Object;

        var parameter = loader.Load(context, descriptor, stringValues, stringValues);

        Assert.Equal(descriptor, parameter.Descriptor);
        Assert.Equal(stringValues, parameter.StringValues);
        Assert.Same(stringValues, parameter.ParsedValue);
    }
}
