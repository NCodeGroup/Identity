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

using System.Buffers;
using System.Text;
using Microsoft.AspNetCore.Http;
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
        var result = new InteractiveHtmlResult(html);

        await result.ExecuteAsync(httpContext);

        Assert.Equal(InteractiveHtmlResult.HtmlContentType, httpContext.Response.ContentType);
        Assert.Equal(Page, Encoding.UTF8.GetString(body.ToArray()));
    }

    #endregion

    #region ContentType Property Tests

    [Fact]
    public void ContentType_Always_IsUtf8Html()
    {
        using var html = InteractiveHtmlResult.CreateBuffer();
        var result = new InteractiveHtmlResult(html);

        Assert.Equal("text/html; charset=utf-8", result.ContentType);
    }

    #endregion
}
