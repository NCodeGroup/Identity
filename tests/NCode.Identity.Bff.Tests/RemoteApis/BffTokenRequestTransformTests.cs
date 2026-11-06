// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using Xunit;
using Yarp.ReverseProxy.Transforms;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public sealed class BffTokenRequestTransformTests : BaseTests
{
    private const string AntiforgeryHeaderName = "X-Custom-CSRF";

    private Mock<IBffTokenAccessor> MockTokens { get; }

    private BffTokenRequest TokenRequest { get; } =
        new(BffTokenRequirementNames.User) { Resource = "https://api.example.com" };

    private BffTokenRequestTransform Transform { get; }

    public BffTokenRequestTransformTests()
    {
        MockTokens = CreateStrictMock<IBffTokenAccessor>();
        Transform = new BffTokenRequestTransform(
            TokenRequest,
            MockTokens.Object,
            BffTestFrontends.CreateCatalog(
                BffTestFrontends.CreateBffOptions(x =>
                    x.Antiforgery.HeaderName = AntiforgeryHeaderName
                )
            )
        );
    }

    private static RequestTransformContext CreateContext(CancellationToken cancellationToken)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/orders");
        proxyRequest.Headers.TryAddWithoutValidation("Cookie", "__Host-Http-bff=handle");
        proxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", "browser");
        proxyRequest.Headers.TryAddWithoutValidation(OpenIdProtocol.DPoP.HeaderName, "proof");
        proxyRequest.Headers.TryAddWithoutValidation(AntiforgeryHeaderName, "1");
        proxyRequest.Headers.TryAddWithoutValidation("X-Trace", "kept");

        return new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
            CancellationToken = cancellationToken,
        };
    }

    private void SetupResult(RequestTransformContext context, BffTokenResult result) =>
        MockTokens
            .Setup(x =>
                x.GetAccessTokenAsync(context.HttpContext, TokenRequest, context.CancellationToken)
            )
            .Returns(new ValueTask<BffTokenResult>(result))
            .Verifiable();

    private static async Task<string?> ReadProblemTypeAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        return problem.GetProperty("type").GetString();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_KeepsTheTokenRequest()
    {
        Assert.Same(TokenRequest, Transform.TokenRequest);
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_TokenResolved_StripsCredentialsAndHandsTheTokenOn()
    {
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext(cancellation.Token);
        var result = BffTokenResult.Success(
            new BffAccessToken("access", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
        );
        SetupResult(context, result);

        await Transform.ApplyAsync(context);

        var headers = context.ProxyRequest.Headers;
        Assert.False(headers.Contains("Cookie"));
        Assert.Null(headers.Authorization);
        Assert.False(headers.Contains(OpenIdProtocol.DPoP.HeaderName));
        Assert.False(headers.Contains(AntiforgeryHeaderName));
        Assert.Equal("kept", Assert.Single(headers.GetValues("X-Trace")));
        Assert.True(
            context.ProxyRequest.Options.TryGetValue(
                BffAccessTokenHandler.ResolvedTokenKey,
                out var resolved
            )
        );
        Assert.Same(result, resolved);
        Assert.Equal(StatusCodes.Status200OK, context.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ApplyAsync_SessionRequired_WritesTheSessionProblem()
    {
        var context = CreateContext(CancellationToken.None);
        SetupResult(
            context,
            BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            )
        );

        await Transform.ApplyAsync(context);

        var response = context.HttpContext.Response;
        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl.ToString());
        Assert.Equal(
            BffProblemTypes.SessionRequired,
            await ReadProblemTypeAsync(context.HttpContext)
        );
        Assert.False(
            context.ProxyRequest.Options.TryGetValue(BffAccessTokenHandler.ResolvedTokenKey, out _)
        );
    }

    [Fact]
    public async Task ApplyAsync_ProviderUnavailable_WritesTheTokenProblem()
    {
        var context = CreateContext(CancellationToken.None);
        SetupResult(
            context,
            BffTokenResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                BffTokenFailureReasons.ProviderUnavailable
            )
        );

        await Transform.ApplyAsync(context);

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            context.HttpContext.Response.StatusCode
        );
        Assert.Equal(
            BffProblemTypes.TokenUnavailable,
            await ReadProblemTypeAsync(context.HttpContext)
        );
    }

    #endregion
}
