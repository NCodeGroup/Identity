// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiTransformerTests : BaseTests
{
    private const string Destination = "https://api.example.com/v1";

    private static BffTokenResult Token { get; } =
        BffTokenResult.Success(
            new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
        );

    private static BffRemoteApiTransformer CreateTransformer(string remainder = "/items/1") =>
        new(new PathString(remainder), Token, BffDefaults.AntiforgeryHeaderName);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.example.com");
        context.Request.Path = "/api/orders/items/1";
        context.Request.QueryString = new QueryString("?expand=lines");
        return context;
    }

    #region TransformRequestAsync Tests

    [Fact]
    public async Task TransformRequestAsync_SetsTheDestinationAddress()
    {
        var context = CreateContext();
        using var proxyRequest = new HttpRequestMessage();

        await CreateTransformer()
            .TransformRequestAsync(context, proxyRequest, Destination, CancellationToken.None);

        Assert.Equal(
            new Uri("https://api.example.com/v1/items/1?expand=lines"),
            proxyRequest.RequestUri
        );
    }

    [Fact]
    public async Task TransformRequestAsync_EmptyRemainder_TargetsTheDestination()
    {
        var context = CreateContext();
        context.Request.QueryString = QueryString.Empty;
        using var proxyRequest = new HttpRequestMessage();

        await CreateTransformer(string.Empty)
            .TransformRequestAsync(context, proxyRequest, Destination, CancellationToken.None);

        Assert.Equal(new Uri("https://api.example.com/v1"), proxyRequest.RequestUri);
    }

    [Fact]
    public async Task TransformRequestAsync_StripsTheBrowsersCredentialsAndForwardingHeaders()
    {
        var context = CreateContext();
        var headers = context.Request.Headers;
        headers.Cookie = "__Host-bff=handle";
        headers.Authorization = "Bearer browser";
        headers[OpenIdProtocol.DPoP.HeaderName] = "browser-proof";
        headers["Forwarded"] = "for=1.2.3.4";
        headers["X-Forwarded-For"] = "1.2.3.4";
        headers["X-Forwarded-Host"] = "evil.example.com";
        headers["X-Forwarded-Proto"] = "http";
        headers["X-Forwarded-Prefix"] = "/evil";
        headers[BffDefaults.AntiforgeryHeaderName] = "1";
        headers.Accept = "application/json";
        headers["X-Custom"] = "kept";
        using var proxyRequest = new HttpRequestMessage();

        await CreateTransformer()
            .TransformRequestAsync(context, proxyRequest, Destination, CancellationToken.None);

        string[] stripped =
        [
            "Cookie",
            "Authorization",
            OpenIdProtocol.DPoP.HeaderName,
            "Forwarded",
            "X-Forwarded-For",
            "X-Forwarded-Host",
            "X-Forwarded-Proto",
            "X-Forwarded-Prefix",
            BffDefaults.AntiforgeryHeaderName,
        ];
        Assert.All(stripped, name => Assert.False(proxyRequest.Headers.Contains(name), name));
        Assert.Equal("application/json", proxyRequest.Headers.Accept.ToString());
        Assert.Equal(["kept"], proxyRequest.Headers.GetValues("X-Custom"));
    }

    [Fact]
    public async Task TransformRequestAsync_DoesNotForwardTheBffsHost()
    {
        var context = CreateContext();
        using var proxyRequest = new HttpRequestMessage();

        await CreateTransformer()
            .TransformRequestAsync(context, proxyRequest, Destination, CancellationToken.None);

        Assert.Null(proxyRequest.Headers.Host);
    }

    [Fact]
    public async Task TransformRequestAsync_PassesTheResolvedToken()
    {
        var context = CreateContext();
        using var proxyRequest = new HttpRequestMessage();

        await CreateTransformer()
            .TransformRequestAsync(context, proxyRequest, Destination, CancellationToken.None);

        Assert.True(
            proxyRequest.Options.TryGetValue(BffAccessTokenHandler.ResolvedTokenKey, out var token)
        );
        Assert.Same(Token, token);
    }

    #endregion

    #region TransformResponseAsync Tests

    [Fact]
    public async Task TransformResponseAsync_DropsTheRemoteApisCookies()
    {
        var context = CreateContext();
        context.Response.Headers.SetCookie = "__Host-bff=renewed";
        using var proxyResponse = new HttpResponseMessage(HttpStatusCode.OK);
        proxyResponse.Headers.TryAddWithoutValidation("Set-Cookie", "tracker=1");
        proxyResponse.Headers.TryAddWithoutValidation("X-Custom", "kept");

        var result = await CreateTransformer()
            .TransformResponseAsync(context, proxyResponse, CancellationToken.None);

        Assert.True(result);
        Assert.Equal("__Host-bff=renewed", context.Response.Headers.SetCookie.ToString());
        Assert.Equal("kept", context.Response.Headers["X-Custom"].ToString());
    }

    [Fact]
    public async Task TransformResponseAsync_NoResponse_ReturnsTheBaseResult()
    {
        var context = CreateContext();

        var result = await CreateTransformer()
            .TransformResponseAsync(context, null, CancellationToken.None);

        Assert.False(result);
    }

    #endregion
}
