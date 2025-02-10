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

using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parameters;

public class KnownParameterTests : BaseTests
{
    [Fact]
    public void Constructor_GivenValid_ThenValid()
    {
        const string parameterName = "parameterName";
        const bool optional = false;

        var mockParser = CreateStrictMock<ParameterParser<string>>();

        var parameter = new KnownParameter<string>(parameterName, mockParser.Object)
        {
            AllowMissingStringValues = optional,
        };

        Assert.Equal(parameterName, parameter.Name);
        Assert.Equal(typeof(string), parameter.ValueType);
        Assert.Equal(optional, parameter.AllowMissingStringValues);
        Assert.Same(mockParser.Object, parameter.Loader);
        Assert.Same(mockParser.Object, parameter.Parser);
    }
}
