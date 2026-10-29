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
        using var document = await JsonDocument.ParseAsync(body);
        var root = document.RootElement;
        Assert.Equal(BffProblemTypes.SessionRequired, root.GetProperty("type").GetString());
        Assert.Equal("Unauthenticated", root.GetProperty("title").GetString());
        Assert.Equal(401, root.GetProperty("status").GetInt32());
    }

    #endregion
}
