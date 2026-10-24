#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using NCode.Identity.OpenId.Authentication.Endpoints;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

public class ContentSecurityPolicyDirectiveTests : BaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WhenGivenANameAndSources_KeepsThemInOrder()
    {
        var directive = new ContentSecurityPolicyDirective(
            ContentSecurityPolicyDirectives.ScriptSrc,
            ContentSecurityPolicySources.Self,
            "https://cdn.example",
            ContentSecurityPolicySources.StrictDynamic
        );

        Assert.Equal("script-src", directive.Name);
        Assert.Equal<string>(
            ["'self'", "https://cdn.example", "'strict-dynamic'"],
            directive.Sources
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("Script-src")]
    [InlineData("script src")]
    [InlineData("script-src;")]
    public void Constructor_WhenTheNameIsInvalid_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ContentSecurityPolicyDirective(name, "'self'")
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("a;b")]
    [InlineData("a,b")]
    [InlineData("a b")]
    [InlineData("caf\u00e9")]
    public void Constructor_WhenASourceIsInvalid_Throws(string source)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ContentSecurityPolicyDirective(
                ContentSecurityPolicyDirectives.ScriptSrc,
                "'self'",
                source
            )
        );
    }

    #endregion
}
