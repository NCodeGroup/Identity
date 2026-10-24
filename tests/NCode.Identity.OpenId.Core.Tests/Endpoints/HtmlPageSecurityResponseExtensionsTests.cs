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

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Authentication.Endpoints;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

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
