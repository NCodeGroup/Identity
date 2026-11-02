// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.Bff.Requests;
using Xunit;

namespace NCode.Identity.Bff.Tests.Requests;

public class BffFormsTests : BaseTests
{
    private static HttpRequest CreateRequest(string? contentType, string body)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = contentType;
        httpContext.Request.Body = new MemoryStream(SecureEncoding.UTF8.GetBytes(body));
        return httpContext.Request;
    }

    #region TryReadFormAsync Tests

    [Fact]
    public async Task TryReadFormAsync_WithAFormBody_ReturnsItsValues()
    {
        var request = CreateRequest("application/x-www-form-urlencoded", "a=1");

        var form = await BffForms.TryReadFormAsync(request, CancellationToken.None);

        Assert.NotNull(form);
        Assert.Equal("1", form["a"].ToString());
    }

    [Fact]
    public async Task TryReadFormAsync_WithoutAFormContentType_ReturnsNull()
    {
        var request = CreateRequest("application/json", "{}");

        var form = await BffForms.TryReadFormAsync(request, CancellationToken.None);

        Assert.Null(form);
    }

    [Fact]
    public async Task TryReadFormAsync_WhenAKeyIsOverTheLimit_ReturnsNull()
    {
        // The form key limit is 2048 characters.
        var request = CreateRequest(
            "application/x-www-form-urlencoded",
            new string('k', 2049) + "=v"
        );

        var form = await BffForms.TryReadFormAsync(request, CancellationToken.None);

        Assert.Null(form);
    }

    #endregion
}
