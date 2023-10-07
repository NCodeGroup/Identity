﻿#region Copyright Preamble
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
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class ParameterParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdMessageContext> MockOpenIdContext { get; }
    private Mock<ITestParameterParser> MockTestParameterParser { get; }

    public ParameterParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdContext = MockRepository.Create<IOpenIdMessageContext>();
        MockTestParameterParser = MockRepository.Create<ITestParameterParser>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Separator_ThenValid()
    {
        var parser = new TestParameterParser(MockTestParameterParser.Object, null, null);

        Assert.Equal(OpenIdConstants.ParameterSeparator, parser.Separator);
    }

    [Fact]
    public void StringComparison_ThenValid()
    {
        var parser = new TestParameterParser(MockTestParameterParser.Object, null, null);

        Assert.Equal(StringComparison.Ordinal, parser.StringComparison);
    }

    [Fact]
    public void Load_ThenValid()
    {
        var parser = new TestParameterParser(MockTestParameterParser.Object, null, null);
        var context = MockOpenIdContext.Object;

        const bool ignoreErrors = false;
        const string parameterName = "parameterName";
        const string stringValues = "stringValues";
        const string parsedValue = "parsedValue";

        var descriptor = new ParameterDescriptor(parameterName);

        MockTestParameterParser
            .Setup(_ => _.Parse(context, descriptor, stringValues, ignoreErrors))
            .Returns(parsedValue)
            .Verifiable();

        var parameter = parser.Load(context, descriptor, stringValues);
        var typedParameter = Assert.IsType<Parameter<string>>(parameter);
        Assert.Equal(stringValues, typedParameter.StringValues);
        Assert.Same(parsedValue, typedParameter.ParsedValue);
    }
}
