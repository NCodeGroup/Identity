// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
