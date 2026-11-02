// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parameters;

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
