// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.Tests.Hosting;

public sealed class BffEndpointStartupCheckTests : BaseTests
{
    private const string TokenRouteId = "orders";
    private const string FrontendRouteId = "pages";

    private List<WebApplication> Apps { get; } = [];

    private BffFrontendCatalog Frontends { get; } = BffTestFrontends.CreateCatalog();

    private FakeLogger<BffEndpointStartupCheck> Logger { get; } = new();

    private IServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var app in Apps)
                ((IDisposable)app).Dispose();
        }

        base.Dispose(disposing);
    }

    private static RouteConfig CreateRoute(string routeId, Dictionary<string, string> metadata) =>
        new()
        {
            RouteId = routeId,
            ClusterId = "upstream",
            Match = new RouteMatch { Path = $"/{routeId}/{{**rest}}" },
            Metadata = metadata,
        };

    private static RouteConfig TokenRoute { get; } =
        CreateRoute(
            TokenRouteId,
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.User,
            }
        );

    private static RouteConfig FrontendRoute { get; } =
        CreateRoute(
            FrontendRouteId,
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.Frontend] = BffTestFrontends.Name,
            }
        );

    private BffTokenContexts CreateContexts()
    {
        var timeProvider = new FakeTimeProvider();
        var cookies = new BffCookies(timeProvider);
        var resolver = new BffSessionResolver(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            [],
            Frontends,
            timeProvider
        );
        return new BffTokenContexts(Frontends, resolver, cookies);
    }

    private Endpoint[] CreateYarpEndpoints(bool asBffRemoteApis, params RouteConfig[] routes)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production }
        );
        var serviceCollection = builder.Services;
        serviceCollection.AddOptions<BffOptions>();

        var requirement = CreateLooseMock<IBffTokenRequirement>();
        requirement.SetupGet(x => x.Name).Returns(BffTokenRequirementNames.User).Verifiable();
        serviceCollection.AddSingleton(requirement.Object);
        serviceCollection.AddSingleton(CreateStrictMock<IBffTokenAccessor>().Object);
        serviceCollection.AddSingleton(CreateStrictMock<IDPoPProofFactory>().Object);
        serviceCollection.AddSingleton<
            IBffConnectionSessionAccessor,
            DefaultBffConnectionSessionAccessor
        >();
        serviceCollection.AddSingleton(CreateContexts());
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.AddSingleton(Frontends);
        var upstreamFactory = CreateStrictMock<IForwarderHttpClientFactory>();
        upstreamFactory
            .Setup(x => x.CreateClient(It.IsAny<ForwarderHttpClientContext>()))
            .Returns(() => new HttpMessageInvoker(new HttpClientHandler()))
            .Verifiable();
        serviceCollection.AddSingleton(upstreamFactory.Object);

        serviceCollection
            .AddReverseProxy()
            .LoadFromMemory(
                routes,
                [
                    new ClusterConfig
                    {
                        ClusterId = "upstream",
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["one"] = new() { Address = "https://upstream.example.com/" },
                        },
                    },
                ]
            )
            .AddBffTransforms();

        var app = builder.Build();
        Apps.Add(app);

        var proxy = app.MapReverseProxy();
        if (asBffRemoteApis)
            proxy.AsBffRemoteApis();

        return
        [
            .. ((IEndpointRouteBuilder)app)
                .DataSources.SelectMany(x => x.Endpoints)
                .OfType<RouteEndpoint>(),
        ];
    }

    private BffTransformProvider CreateTransformProvider()
    {
        var clientFactory = new BffForwarderHttpClientFactory(
            CreateStrictMock<IForwarderHttpClientFactory>().Object,
            CreateStrictMock<IHttpContextAccessor>().Object,
            new DefaultBffConnectionSessionAccessor(),
            CreateContexts(),
            CreateStrictMock<IBffTokenAccessor>().Object,
            CreateStrictMock<IDPoPProofFactory>().Object
        );

        return new BffTransformProvider(
            clientFactory,
            [],
            CreateStrictMock<IBffTokenAccessor>().Object,
            Frontends
        );
    }

    private BffEndpointStartupCheck CreateCheck(
        EndpointDataSource endpoints,
        bool hasBffTransforms = true
    ) =>
        new(
            endpoints,
            Frontends,
            Services,
            hasBffTransforms ? [CreateTransformProvider()] : [],
            Logger
        );

    private static Endpoint CreateFrontendEndpoint(string name) =>
        new(null, new EndpointMetadataCollection(new BffFrontendAttribute(name)), "stub endpoint");

    private static Endpoint CreateRequirementEndpoint(IBffEndpointStartupRequirement requirement) =>
        new(null, new EndpointMetadataCollection(requirement), "requirement endpoint");

    #region FindFailures Tests

    [Fact]
    public void FindFailures_WhenARemoteApiRouteIsNotMarkedAsABffApi_NamesTheRouteAndTheConvention()
    {
        var endpoints = CreateYarpEndpoints(asBffRemoteApis: false, TokenRoute);

        var failure = Assert.Single(
            BffEndpointStartupCheck.FindFailures(
                endpoints,
                Frontends,
                Services,
                hasBffTransforms: true
            )
        );

        Assert.Contains($"'{TokenRouteId}'", failure);
        Assert.Contains(BffReverseProxyMetadataKeys.TokenRequirement, failure);
        Assert.Contains("AsBffRemoteApis()", failure);
    }

    [Fact]
    public void FindFailures_WhenARemoteApiRouteIsMarkedAsABffApi_ReturnsNone()
    {
        var endpoints = CreateYarpEndpoints(asBffRemoteApis: true, TokenRoute);

        var failures = BffEndpointStartupCheck.FindFailures(
            endpoints,
            Frontends,
            Services,
            hasBffTransforms: true
        );

        Assert.Empty(failures);
    }

    [Fact]
    public void FindFailures_WhenTheBffTransformsAreMissing_NamesTheRouteAndAddBffTransforms()
    {
        var endpoints = CreateYarpEndpoints(asBffRemoteApis: true, TokenRoute);

        var failure = Assert.Single(
            BffEndpointStartupCheck.FindFailures(
                endpoints,
                Frontends,
                Services,
                hasBffTransforms: false
            )
        );

        Assert.Contains($"'{TokenRouteId}'", failure);
        Assert.Contains("AddBffTransforms()", failure);
    }

    [Fact]
    public void FindFailures_WhenAFrontendRouteIsNotLimitedToItsFrontend_NamesTheRouteAndTheConvention()
    {
        var endpoints = CreateYarpEndpoints(asBffRemoteApis: false, FrontendRoute);

        var failure = Assert.Single(
            BffEndpointStartupCheck.FindFailures(
                endpoints,
                Frontends,
                Services,
                hasBffTransforms: true
            )
        );

        Assert.Contains($"'{FrontendRouteId}'", failure);
        Assert.Contains(BffReverseProxyMetadataKeys.Frontend, failure);
        Assert.Contains($"'{BffTestFrontends.Name}'", failure);
        Assert.Contains("AsBffRemoteApis()", failure);
    }

    [Fact]
    public void FindFailures_WhenAFrontendRouteIsLimitedToItsFrontend_ReturnsNone()
    {
        var endpoints = CreateYarpEndpoints(asBffRemoteApis: true, FrontendRoute);

        var failures = BffEndpointStartupCheck.FindFailures(
            endpoints,
            Frontends,
            Services,
            hasBffTransforms: true
        );

        Assert.Empty(failures);
    }

    [Fact]
    public void FindFailures_WhenAnEndpointNamesAnUnconfiguredFrontend_NamesTheEndpointAndTheFrontend()
    {
        var failure = Assert.Single(
            BffEndpointStartupCheck.FindFailures(
                [CreateFrontendEndpoint("missing")],
                Frontends,
                Services,
                hasBffTransforms: true
            )
        );

        Assert.Contains("'stub endpoint'", failure);
        Assert.Contains("'missing'", failure);
        Assert.Contains("Bff:Frontends", failure);
    }

    [Fact]
    public void FindFailures_WhenAnEndpointNamesAConfiguredFrontend_ReturnsNone()
    {
        var failures = BffEndpointStartupCheck.FindFailures(
            [CreateFrontendEndpoint(BffTestFrontends.Name)],
            Frontends,
            Services,
            hasBffTransforms: true
        );

        Assert.Empty(failures);
    }

    [Fact]
    public void FindFailures_RequirementFails_ReportsIt()
    {
        var requirement = CreateStrictMock<IBffEndpointStartupRequirement>();
        requirement
            .Setup(x => x.Validate(Services))
            .Returns("It needs a registration.")
            .Verifiable();

        var failure = Assert.Single(
            BffEndpointStartupCheck.FindFailures(
                [CreateRequirementEndpoint(requirement.Object)],
                Frontends,
                Services,
                hasBffTransforms: true
            )
        );

        Assert.Equal("Endpoint 'requirement endpoint': It needs a registration.", failure);
    }

    [Fact]
    public void FindFailures_RequirementHolds_ReportsNothing()
    {
        var requirement = CreateStrictMock<IBffEndpointStartupRequirement>();
        requirement.Setup(x => x.Validate(Services)).Returns((string?)null).Verifiable();

        var failures = BffEndpointStartupCheck.FindFailures(
            [CreateRequirementEndpoint(requirement.Object)],
            Frontends,
            Services,
            hasBffTransforms: true
        );

        Assert.Empty(failures);
    }

    #endregion

    #region StartingAsync Tests

    [Fact]
    public void StartingAsync_Always_Completes()
    {
        using var check = CreateCheck(new DefaultEndpointDataSource());

        var task = check.StartingAsync(CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public void StartAsync_Always_Completes()
    {
        using var check = CreateCheck(new DefaultEndpointDataSource());

        var task = check.StartAsync(CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
    }

    #endregion

    #region StartedAsync Tests

    [Fact]
    public async Task StartedAsync_WhenAnEndpointIsIncomplete_Throws()
    {
        using var check = CreateCheck(
            new DefaultEndpointDataSource(CreateFrontendEndpoint("missing"))
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            check.StartedAsync(CancellationToken.None)
        );

        Assert.Contains("'missing'", exception.Message);
    }

    [Fact]
    public async Task StartedAsync_WhenTheEndpointsAreComplete_Completes()
    {
        using var check = CreateCheck(
            new DefaultEndpointDataSource(CreateFrontendEndpoint(BffTestFrontends.Name))
        );

        await check.StartedAsync(CancellationToken.None);

        Assert.Equal(0, Logger.Collector.Count);
    }

    [Fact]
    public async Task StartedAsync_WhenTheEndpointsChangeToIncomplete_LogsEachFailureAsAnError()
    {
        var source = new StubEndpointDataSource(CreateFrontendEndpoint(BffTestFrontends.Name));
        using var check = CreateCheck(source);
        await check.StartedAsync(CancellationToken.None);

        source.Change(CreateFrontendEndpoint("missing"));

        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("'missing'", record.Message);
    }

    [Fact]
    public async Task StartedAsync_WhenTheEndpointsChangeToComplete_LogsNothing()
    {
        var source = new StubEndpointDataSource(CreateFrontendEndpoint(BffTestFrontends.Name));
        using var check = CreateCheck(source);
        await check.StartedAsync(CancellationToken.None);

        source.Change(CreateFrontendEndpoint(BffTestFrontends.Name));

        Assert.Equal(0, Logger.Collector.Count);
    }

    #endregion

    #region StoppingAsync Tests

    [Fact]
    public async Task StoppingAsync_WhenTheEndpointsChangeAfterwards_LogsNothing()
    {
        var source = new StubEndpointDataSource(CreateFrontendEndpoint(BffTestFrontends.Name));
        using var check = CreateCheck(source);
        await check.StartedAsync(CancellationToken.None);

        await check.StoppingAsync(CancellationToken.None);
        source.Change(CreateFrontendEndpoint("missing"));

        Assert.Equal(0, Logger.Collector.Count);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public void StopAsync_Always_Completes()
    {
        using var check = CreateCheck(new DefaultEndpointDataSource());

        var task = check.StopAsync(CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
    }

    #endregion

    #region StoppedAsync Tests

    [Fact]
    public void StoppedAsync_Always_Completes()
    {
        using var check = CreateCheck(new DefaultEndpointDataSource());

        var task = check.StoppedAsync(CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
    }

    #endregion

    private sealed class StubEndpointDataSource(Endpoint endpoint) : EndpointDataSource
    {
        private CancellationTokenSource TokenSource { get; set; } = new();

        private IReadOnlyList<Endpoint> Current { get; set; } = [endpoint];

        public override IReadOnlyList<Endpoint> Endpoints => Current;

        public override IChangeToken GetChangeToken() =>
            new CancellationChangeToken(TokenSource.Token);

        public void Change(Endpoint replacement)
        {
            Current = [replacement];
            var previous = TokenSource;
            TokenSource = new CancellationTokenSource();
            previous.Cancel();
            previous.Dispose();
        }
    }
}
