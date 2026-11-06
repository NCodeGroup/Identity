// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.Tests.RemoteApis;

/// <summary>
/// Forwards requests through YARP's real forwarder, the remote API transformer and the access token handler, to a
/// stub remote API.
/// </summary>
public sealed class BffRemoteApiForwardingTests : BaseTests
{
    private const string Jkt = "thumbprint";
    private static readonly Uri RemoteUri = new("https://api.example.com/v1/items/1?expand=lines");

    private ServiceProvider Provider { get; } =
        new ServiceCollection().AddLogging().AddHttpForwarder().BuildServiceProvider();

    private Mock<IBffTokenAccessor> MockTokens { get; }

    private Mock<IDPoPProofFactory> MockProofs { get; }

    private BffFrontendCatalog Frontends { get; }

    private BffTokenContexts Contexts { get; }

    private BffTokenRequest TokenRequest { get; } = new(BffTokenRequirementNames.User);

    public BffRemoteApiForwardingTests()
    {
        MockTokens = CreateStrictMock<IBffTokenAccessor>();
        MockProofs = CreateStrictMock<IDPoPProofFactory>();
        Frontends = BffTestFrontends.CreateCatalog(x =>
            x.DPoP = new BffDPoPOptions { Mode = BffDPoPModes.Required, KeyRing = "dpop-keys" }
        );

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 2, 5, 0, 0, TimeSpan.Zero)
        );
        var cookies = new BffCookies(timeProvider);
        var resolver = new BffSessionResolver(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            [],
            Frontends,
            timeProvider
        );
        Contexts = new BffTokenContexts(Frontends, resolver, cookies);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Provider.Dispose();
        base.Dispose(disposing);
    }

    private BffRemoteApi CreateApi(StubHttpMessageHandler remote, HttpContext? context = null)
    {
        // The token handler reads the accessor only to sign a DPoP proof: the transformer hands it the resolved token.
        var accessor = CreateStrictMock<IHttpContextAccessor>();
        if (context is not null)
            accessor.SetupGet(x => x.HttpContext).Returns(context).Verifiable();

        var handler = new BffAccessTokenHandler(
            accessor.Object,
            new DefaultBffConnectionSessionAccessor(),
            Contexts,
            MockTokens.Object,
            MockProofs.Object,
            TokenRequest
        )
        {
            InnerHandler = remote,
        };
        return new BffRemoteApi(
            "/api/orders",
            "https://api.example.com/v1",
            TokenRequest,
            [],
            TimeSpan.FromSeconds(30),
            new HttpMessageInvoker(handler)
        );
    }

    private BffRemoteApiHandler CreateHandler() =>
        new(
            Provider.GetRequiredService<IHttpForwarder>(),
            MockTokens.Object,
            Microsoft.Extensions.Options.Options.Create(new BffOptions()),
            NullLogger<BffRemoteApiHandler>.Instance
        );

    private static DefaultHttpContext CreateContext(string method, string? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.example.com");
        context.Request.Path = "/api/orders/items/1";
        context.Request.RouteValues[BffRemoteApiPaths.RemainderParameter] = "items/1";
        context.Request.QueryString = new QueryString("?expand=lines");
        context.Request.Headers.Cookie = "__Host-bff=handle";
        context.Request.Headers[BffDefaults.AntiforgeryHeaderName] = "1";
        if (body is not null)
        {
            var bytes = SecureEncoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Request.ContentType = "application/json";
        }

        context.Response.Body = new MemoryStream();
        return context;
    }

    private void SetupToken(HttpContext context, BffAccessToken token) =>
        MockTokens
            .Setup(x => x.GetAccessTokenAsync(context, TokenRequest, context.RequestAborted))
            .Returns(new ValueTask<BffTokenResult>(BffTokenResult.Success(token)))
            .Verifiable();

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, SecureEncoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static HttpResponseMessage CreateRemoteResponse(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, SecureEncoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("Set-Cookie", "tracker=1");
        return response;
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_BearerToken_ForwardsTheRequestAndCopiesTheResponse()
    {
        var context = CreateContext(HttpMethods.Post, """{"qty":1}""");
        SetupToken(
            context,
            new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
        );
        HttpRequestHeaders? sentHeaders = null;
        var remote = new StubHttpMessageHandler(
            (request, _) =>
            {
                sentHeaders = request.Headers;
                return Task.FromResult(
                    CreateRemoteResponse(HttpStatusCode.Created, """{"id":7}""")
                );
            }
        );

        await CreateHandler().HandleAsync(context, CreateApi(remote));

        var recorded = Assert.Single(remote.Requests);
        Assert.Equal(HttpMethod.Post, recorded.Method);
        Assert.Equal(RemoteUri, recorded.RequestUri);
        Assert.Equal("Bearer at", recorded.Authorization);
        Assert.Equal("""{"qty":1}""", recorded.Body);
        Assert.NotNull(sentHeaders);
        Assert.False(sentHeaders.Contains("Cookie"));
        Assert.False(sentHeaders.Contains(BffDefaults.AntiforgeryHeaderName));
        Assert.Null(sentHeaders.Host);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal("""{"id":7}""", await ReadResponseBodyAsync(context));
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task HandleAsync_DPoPNonceChallenge_ReplaysTheBufferedBody()
    {
        var context = CreateContext(HttpMethods.Post, """{"qty":1}""");
        SetupToken(
            context,
            new BffAccessToken("at", OpenIdProtocol.TokenTypes.DPoP, DateTimeOffset.MaxValue)
            {
                DPoPJwkThumbprint = Jkt,
            }
        );
        var proofs = new Queue<string>(["proof-1", "proof-2"]);
        MockProofs
            .Setup(x =>
                x.CreateResourceProofAsync(
                    BffTestFrontends.Get(Frontends).Registration,
                    Jkt,
                    HttpMethod.Post,
                    RemoteUri,
                    "at",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => new ValueTask<string>(proofs.Dequeue()))
            .Verifiable();
        MockProofs.Setup(x => x.RecordNonce(RemoteUri, "nonce-1")).Verifiable();

        var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        challenge.Headers.TryAddWithoutValidation(
            "WWW-Authenticate",
            $"DPoP error=\"{OpenIdProtocol.ErrorCodes.UseDPoPNonce}\""
        );
        challenge.Headers.TryAddWithoutValidation(OpenIdProtocol.DPoP.NonceHeaderName, "nonce-1");
        var remote = StubHttpMessageHandler.Sequence(
            challenge,
            CreateRemoteResponse(HttpStatusCode.OK, """{"id":7}""")
        );

        await CreateHandler().HandleAsync(context, CreateApi(remote, context));

        Assert.Equal(2, remote.Requests.Count);
        Assert.Equal("proof-1", remote.Requests[0].DPoP);
        Assert.Equal("proof-2", remote.Requests[1].DPoP);
        Assert.Equal("""{"qty":1}""", remote.Requests[0].Body);
        Assert.Equal("""{"qty":1}""", remote.Requests[1].Body);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("""{"id":7}""", await ReadResponseBodyAsync(context));
    }

    [Fact]
    public async Task HandleAsync_RemoteApiUnreachable_Returns502()
    {
        var context = CreateContext(HttpMethods.Get);
        SetupToken(
            context,
            new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
        );
        var remote = new StubHttpMessageHandler(
            (_, _) => throw new HttpRequestException("connection refused")
        );

        await CreateHandler().HandleAsync(context, CreateApi(remote));

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
    }

    #endregion
}
