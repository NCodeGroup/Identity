// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public class HtmlPageSecurityResponseExtensionsTests : BaseTests
{
    #region SetHtmlPageSecurity Tests

    [Fact]
    public void SetHtmlPageSecurity_Always_WritesThePolicyAndTheSecurityHeaders()
    {
        var response = new DefaultHttpContext().Response;
        var policy = new HtmlPageSecurityPolicy
        {
            Directives =
            [
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ScriptSrc,
                    ContentSecurityPolicySources.Self
                ),
            ],
        };

        response.SetHtmlPageSecurity(policy);

        var headers = response.Headers;
        Assert.Equal(policy.ToContentSecurityPolicy(), headers.ContentSecurityPolicy.ToString());
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("same-origin", headers["Referrer-Policy"].ToString());
        Assert.Equal("no-store, no-cache, max-age=0", headers.CacheControl.ToString());
        Assert.Equal("no-cache", headers.Pragma.ToString());
    }

    [Fact]
    public void SetHtmlPageSecurity_WhenAnotherComponentAllowedSameOrigin_ReplacesItWithDeny()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers.XFrameOptions = "SAMEORIGIN";

        response.SetHtmlPageSecurity(new HtmlPageSecurityPolicy());

        Assert.Equal("DENY", response.Headers.XFrameOptions.ToString());
    }

    [Fact]
    public void SetHtmlPageSecurity_WhenThePageAllowsFraming_SendsNoFramingHeader()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers.XFrameOptions = "SAMEORIGIN";

        response.SetHtmlPageSecurity(new HtmlPageSecurityPolicy { AllowsFraming = true });

        Assert.False(response.Headers.ContainsKey(HeaderNames.XFrameOptions));
    }

    [Fact]
    public void SetHtmlPageSecurity_WhenThePageNamesItsFramers_SendsNoFramingHeader()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers.XFrameOptions = "SAMEORIGIN";

        response.SetHtmlPageSecurity(
            new HtmlPageSecurityPolicy { FrameAncestors = ["https://rp.example"] }
        );

        var headers = response.Headers;
        Assert.False(headers.ContainsKey(HeaderNames.XFrameOptions));
        Assert.EndsWith(
            "frame-ancestors https://rp.example",
            headers.ContentSecurityPolicy.ToString()
        );
    }

    [Fact]
    public void SetHtmlPageSecurity_WhenThePolicyIsNull_Throws()
    {
        var response = new DefaultHttpContext().Response;

        Assert.Throws<ArgumentNullException>(() => response.SetHtmlPageSecurity(null!));
    }

    #endregion
}
