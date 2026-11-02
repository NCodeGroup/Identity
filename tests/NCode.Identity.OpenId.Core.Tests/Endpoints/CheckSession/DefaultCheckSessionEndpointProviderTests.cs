// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Endpoints.CheckSession;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.CheckSession;

public class DefaultCheckSessionEndpointProviderTests : BaseTests
{
    #region HandleRouteAsync Tests

    [Fact]
    public void HandleRouteAsync_Always_MayBeFramedAndRunsOnlyItsScript()
    {
        var httpContext = new DefaultHttpContext();

        var result = new DefaultCheckSessionEndpointProvider().HandleRouteAsync(httpContext);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.NotNull(content.ResponseContent);

        const string scriptStart = "<script>";
        var start =
            content.ResponseContent.IndexOf(scriptStart, StringComparison.Ordinal)
            + scriptStart.Length;
        var end = content.ResponseContent.IndexOf("</script>", start, StringComparison.Ordinal);
        var script = content.ResponseContent[start..end];
        var hash = Convert.ToBase64String(SHA256.HashData(SecureEncoding.UTF8.GetBytes(script)));

        var headers = httpContext.Response.Headers;
        Assert.Equal(
            $"default-src 'none'; script-src 'sha256-{hash}'; base-uri 'none'; form-action 'none'",
            headers.ContentSecurityPolicy.ToString()
        );
        Assert.False(headers.ContainsKey(HeaderNames.XFrameOptions));
    }

    #endregion
}
