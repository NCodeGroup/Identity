// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.AspNetCore;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractiveHtmlResultTests
{
    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_Always_WritesTheHtmlAsUtf8()
    {
        const string Page = "<p>Grüße — 你好</p>";
        using var body = new MemoryStream();
        var httpContext = new DefaultHttpContext { Response = { Body = body } };
        var html = InteractiveHtmlResult.CreateBuffer();
        html.Write(Page);
        var result = new InteractiveHtmlResult(html, new HtmlPageSecurityPolicy());

        await result.ExecuteAsync(httpContext);

        Assert.Equal(InteractiveHtmlResult.HtmlContentType, httpContext.Response.ContentType);
        Assert.Equal(Page, SecureEncoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task ExecuteAsync_Always_ServesThePageUnderItsPolicy()
    {
        using var body = new MemoryStream();
        var httpContext = new DefaultHttpContext { Response = { Body = body } };
        var html = InteractiveHtmlResult.CreateBuffer();
        html.Write("<p>page</p>");
        var policy = new HtmlPageSecurityPolicy
        {
            Directives = [new(ContentSecurityPolicyDirectives.ScriptSrc, "'nonce-n0nce'")],
        };
        var result = new InteractiveHtmlResult(html, policy);

        await result.ExecuteAsync(httpContext);

        var headers = httpContext.Response.Headers;
        Assert.Equal(policy.ToContentSecurityPolicy(), headers.ContentSecurityPolicy.ToString());
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
    }

    #endregion

    #region ContentType Property Tests

    [Fact]
    public void ContentType_Always_IsUtf8Html()
    {
        using var html = InteractiveHtmlResult.CreateBuffer();
        var result = new InteractiveHtmlResult(html, new HtmlPageSecurityPolicy());

        Assert.Equal("text/html; charset=utf-8", result.ContentType);
    }

    #endregion
}
