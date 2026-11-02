// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Responses;
using Xunit;

namespace NCode.Identity.Bff.Tests.Responses;

public class BffResponsesTests : BaseTests
{
    #region SetNoStore Tests

    [Fact]
    public void SetNoStore_Always_SetsCacheControl()
    {
        var httpContext = new DefaultHttpContext();

        BffResponses.SetNoStore(httpContext.Response);

        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());
    }

    #endregion

    #region WriteProblemAsync Tests

    [Fact]
    public async Task WriteProblemAsync_Unauthorized_WritesProblemJson()
    {
        var httpContext = new DefaultHttpContext();
        using var body = new MemoryStream();
        httpContext.Response.Body = body;

        await BffResponses.WriteProblemAsync(
            httpContext,
            401,
            BffProblemTypes.SessionRequired,
            "Unauthenticated"
        );

        Assert.Equal(401, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());

        body.Position = 0;
        var root = await JsonSerializer.DeserializeAsync<JsonElement>(body);
        Assert.Equal(BffProblemTypes.SessionRequired, root.GetProperty("type").GetString());
        Assert.Equal("Unauthenticated", root.GetProperty("title").GetString());
        Assert.Equal(401, root.GetProperty("status").GetInt32());
    }

    #endregion
}
