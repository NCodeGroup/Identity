// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.AspNetCore.Tests;

public class HtmlPageSecurityPolicyTests : BaseTests
{
    #region ToContentSecurityPolicy Tests

    [Fact]
    public void ToContentSecurityPolicy_WhenNothingIsAllowed_ReturnsTheStrictDefault()
    {
        var policy = new HtmlPageSecurityPolicy();

        var actual = policy.ToContentSecurityPolicy();

        Assert.Equal(
            "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            actual
        );
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenTheDirectivesAreTheDefaultArray_ReturnsTheStrictDefault()
    {
        var policy = new HtmlPageSecurityPolicy { Directives = default };

        var actual = policy.ToContentSecurityPolicy();

        Assert.Equal(
            "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            actual
        );
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenDirectivesAreListed_WritesThemInOrder()
    {
        var policy = new HtmlPageSecurityPolicy
        {
            Directives =
            [
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ScriptSrc,
                    ContentSecurityPolicySources.Self
                ),
                new ContentSecurityPolicyDirective(ContentSecurityPolicyDirectives.FrameSrc),
            ],
        };

        var actual = policy.ToContentSecurityPolicy();

        Assert.Equal(
            "default-src 'none'; script-src 'self'; frame-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            actual
        );
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenTheBaseAndFormActionAreListed_ReplacesTheirDefaults()
    {
        var policy = new HtmlPageSecurityPolicy
        {
            Directives =
            [
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.BaseUri,
                    ContentSecurityPolicySources.Self
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.FormAction,
                    ContentSecurityPolicySources.Self
                ),
            ],
        };

        var actual = policy.ToContentSecurityPolicy();

        Assert.Equal(
            "default-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'",
            actual
        );
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenAnyFormActionIsAllowed_OmitsFormAction()
    {
        var policy = new HtmlPageSecurityPolicy { AllowsAnyFormAction = true };

        var actual = policy.ToContentSecurityPolicy();

        Assert.Equal("default-src 'none'; base-uri 'none'; frame-ancestors 'none'", actual);
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenFramingIsAllowed_OmitsFrameAncestors()
    {
        var policy = new HtmlPageSecurityPolicy { AllowsFraming = true };

        var actual = policy.ToContentSecurityPolicy();

        Assert.Equal("default-src 'none'; base-uri 'none'; form-action 'none'", actual);
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenFrameAncestorsAreListed_WritesThemInPlaceOfNone()
    {
        var policy = new HtmlPageSecurityPolicy
        {
            FrameAncestors = ["https://rp.example", "https://other.example:8443"],
        };

        var actual = policy.ToContentSecurityPolicy();

        Assert.Equal(
            "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors https://rp.example https://other.example:8443",
            actual
        );
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenFramingIsAllowedAndFrameAncestorsAreListed_Throws()
    {
        var policy = new HtmlPageSecurityPolicy
        {
            AllowsFraming = true,
            FrameAncestors = ["https://rp.example"],
        };

        Assert.Throws<InvalidOperationException>(() => policy.ToContentSecurityPolicy());
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenAFrameAncestorIsInvalid_Throws()
    {
        var policy = new HtmlPageSecurityPolicy { FrameAncestors = ["a;b"] };

        Assert.ThrowsAny<ArgumentException>(() => policy.ToContentSecurityPolicy());
    }

    [Theory]
    [InlineData(ContentSecurityPolicyDirectives.DefaultSrc)]
    [InlineData(ContentSecurityPolicyDirectives.FrameAncestors)]
    public void ToContentSecurityPolicy_WhenAReservedDirectiveIsListed_Throws(string name)
    {
        var policy = new HtmlPageSecurityPolicy
        {
            Directives =
            [
                new ContentSecurityPolicyDirective(name, ContentSecurityPolicySources.Self),
            ],
        };

        Assert.Throws<InvalidOperationException>(() => policy.ToContentSecurityPolicy());
    }

    [Fact]
    public void ToContentSecurityPolicy_WhenADirectiveIsListedTwice_Throws()
    {
        var policy = new HtmlPageSecurityPolicy
        {
            Directives =
            [
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ScriptSrc,
                    ContentSecurityPolicySources.Self
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ScriptSrc,
                    ContentSecurityPolicySources.None
                ),
            ],
        };

        Assert.Throws<InvalidOperationException>(() => policy.ToContentSecurityPolicy());
    }

    #endregion
}
