// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace NCode.Identity.Bff.Tests.RemoteApis;

/// <summary>
/// Forwards requests through YARP's configured routes, the BFF's transform provider and its client factory decorator,
/// to a stub upstream.
/// </summary>
public sealed class BffReverseProxyForwardingTests : BaseTests
{
    private const string Destination = "https://upstream.example.com/";
    private const string TokenRouteId = "orders";
    private const string OtherRouteId = "pages";

    private WebApplication? App { get; set; }

    private Mock<IBffTokenAccessor> MockTokens { get; }

    private HttpRequestHeaders? SentHeaders { get; set; }

    private StubHttpMessageHandler Upstream { get; }

    public BffReverseProxyForwardingTests()
    {
        MockTokens = CreateStrictMock<IBffTokenAccessor>();
        Upstream = new StubHttpMessageHandler(
            (request, _) =>
            {
                SentHeaders = request.Headers;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok", SecureEncoding.UTF8),
                };
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    "__Host-Http-bff=forged; Path=/; Secure"
                );
                response.Headers.TryAddWithoutValidation("Set-Cookie", "theme=dark; Path=/");
                return Task.FromResult(response);
            }
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && App is not null)
            ((IDisposable)App).Dispose();
        base.Dispose(disposing);
    }

    private static RouteConfig CreateRoute(
        string routeId,
        string path,
        Dictionary<string, string>? metadata
    ) =>
        new()
        {
            RouteId = routeId,
            ClusterId = "upstream",
            Match = new RouteMatch { Path = path },
            Metadata = metadata,
        };

    private static RouteConfig TokenRoute(string requirement) =>
        CreateRoute(
            TokenRouteId,
            "/api/orders/{**rest}",
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = requirement,
                [BffReverseProxyMetadataKeys.Scope] = "orders.read",
            }
        );

    private static RouteConfig OtherRoute { get; } =
        CreateRoute(OtherRouteId, "/pages/{**rest}", null);

    private IEndpointRouteBuilder CreateApp(params RouteConfig[] routes)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production }
        );
        var services = builder.Services;
        services.AddOptions<BffOptions>();

        var requirement = CreateLooseMock<IBffTokenRequirement>();
        requirement.SetupGet(x => x.Name).Returns(BffTokenRequirementNames.User);
        services.AddSingleton(requirement.Object);
        services.AddSingleton(MockTokens.Object);
        services.AddSingleton(CreateStrictMock<IDPoPProofFactory>().Object);
        services.AddHttpContextAccessor();
        services.AddSingleton<BffClientRegistrationProvider>();

        var upstreamFactory = CreateStrictMock<IForwarderHttpClientFactory>();
        upstreamFactory
            .Setup(x => x.CreateClient(It.IsAny<ForwarderHttpClientContext>()))
            .Returns(() => new HttpMessageInvoker(Upstream, disposeHandler: false));

        var proxy = services
            .AddReverseProxy()
            .LoadFromMemory(
                routes,
                [
                    new ClusterConfig
                    {
                        ClusterId = "upstream",
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["one"] = new() { Address = Destination },
                        },
                    },
                ]
            );
        services.AddSingleton(upstreamFactory.Object);
        proxy.AddBffTransforms();

        App = builder.Build();
        App.MapReverseProxy().AsBffRemoteApis();
        return App;
    }

    private static RouteEndpoint GetEndpoint(IEndpointRouteBuilder endpoints, string routeId) =>
        endpoints
            .DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.Metadata.GetMetadata<RouteModel>()?.Config.RouteId == routeId
            );

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.example.com");
        context.Request.Path = path;
        context.Request.Headers.Cookie = "theme=dark; __Host-Http-bff=handle";
        context.Request.Headers.Authorization = "Basic browser";
        context.Request.Headers[BffDefaults.AntiforgeryHeaderName] = "1";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ForwarderError> ForwardAsync(
        IEndpointRouteBuilder endpoints,
        string routeId,
        HttpContext context
    )
    {
        var route = GetEndpoint(endpoints, routeId).Metadata.GetRequiredMetadata<RouteModel>();
        var cluster = route.Cluster;
        Assert.NotNull(cluster);

        context.RequestServices = endpoints.ServiceProvider;
        var forwarder = endpoints.ServiceProvider.GetRequiredService<IHttpForwarder>();
        return await forwarder.SendAsync(
            context,
            Destination,
            cluster.Model.HttpClient,
            ForwarderRequestConfig.Empty,
            route.Transformer
        );
    }

    private void SetupToken(HttpContext context, BffTokenResult result) =>
        MockTokens
            .Setup(x =>
                x.GetAccessTokenAsync(
                    context,
                    It.Is<BffTokenRequest>(request =>
                        request.Requirement == BffTokenRequirementNames.User
                        && request.Resource == null
                        && request.Scope == "orders.read"
                    ),
                    // YARP hands its transforms a token linked to RequestAborted and the activity timeout.
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<BffTokenResult>(result))
            .Verifiable();

    #region MapReverseProxy Tests

    [Fact]
    public void MapReverseProxy_AsBffRemoteApis_MarksOnlyTokenRoutesAsBffApis()
    {
        var endpoints = CreateApp(TokenRoute(BffTokenRequirementNames.User), OtherRoute);

        Assert.NotNull(
            GetEndpoint(endpoints, TokenRouteId).Metadata.GetMetadata<BffApiAttribute>()
        );
        Assert.Null(GetEndpoint(endpoints, OtherRouteId).Metadata.GetMetadata<BffApiAttribute>());
    }

    [Fact]
    public void MapReverseProxy_UnregisteredRequirement_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateApp(TokenRoute("unknown"))
        );

        var errors = Assert.IsType<AggregateException>(exception.InnerException);
        var error = Assert.Single(errors.InnerExceptions);
        Assert.Equal(
            "The metadata of route 'orders' is invalid: BffTokenRequirement 'unknown' is not a registered token requirement.",
            error.Message
        );
    }

    #endregion

    #region Forwarding Tests

    [Fact]
    public async Task Forwarding_TokenRoute_AttachesTheTokenAndStripsBrowserCredentials()
    {
        var endpoints = CreateApp(TokenRoute(BffTokenRequirementNames.User), OtherRoute);
        var context = CreateContext("/api/orders/7");
        SetupToken(
            context,
            BffTokenResult.Success(
                new BffAccessToken(
                    "access",
                    OpenIdProtocol.TokenTypes.Bearer,
                    DateTimeOffset.MaxValue
                )
            )
        );

        var error = await ForwardAsync(endpoints, TokenRouteId, context);

        Assert.Equal(ForwarderError.None, error);
        var recorded = Assert.Single(Upstream.Requests);
        Assert.Equal(new Uri("https://upstream.example.com/api/orders/7"), recorded.RequestUri);
        Assert.Equal($"{OpenIdProtocol.TokenTypes.Bearer} access", recorded.Authorization);
        Assert.NotNull(SentHeaders);
        Assert.False(SentHeaders.Contains("Cookie"));
        Assert.False(SentHeaders.Contains(BffDefaults.AntiforgeryHeaderName));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Forwarding_TokenUnavailable_AnswersTheProblemWithoutForwarding()
    {
        var endpoints = CreateApp(TokenRoute(BffTokenRequirementNames.User));
        var context = CreateContext("/api/orders/7");
        SetupToken(
            context,
            BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            )
        );

        await ForwardAsync(endpoints, TokenRouteId, context);

        Assert.Empty(Upstream.Requests);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        Assert.Equal(BffProblemTypes.SessionRequired, problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Forwarding_OtherRoute_RemovesOnlyTheBffCookies()
    {
        var endpoints = CreateApp(OtherRoute);
        var context = CreateContext("/pages/home");

        var error = await ForwardAsync(endpoints, OtherRouteId, context);

        Assert.Equal(ForwarderError.None, error);
        var recorded = Assert.Single(Upstream.Requests);
        Assert.Equal("Basic browser", recorded.Authorization);
        Assert.NotNull(SentHeaders);
        Assert.Equal("theme=dark", Assert.Single(SentHeaders.GetValues("Cookie")));
        Assert.Equal("theme=dark; Path=/", Assert.Single(context.Response.Headers.SetCookie));
    }

    #endregion
}
