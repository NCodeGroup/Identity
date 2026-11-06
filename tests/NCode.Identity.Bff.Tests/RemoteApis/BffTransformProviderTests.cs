// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.OpenId.Client;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public sealed class BffTransformProviderTests : BaseTests
{
    private const string AntiforgeryHeaderName = "X-Custom-CSRF";

    private ServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();

    private Mock<IBffTokenAccessor> MockTokens { get; }

    private BffForwarderHttpClientFactory ClientFactory { get; }

    public BffTransformProviderTests()
    {
        MockTokens = CreateStrictMock<IBffTokenAccessor>();
        ClientFactory = new BffForwarderHttpClientFactory(
            CreateStrictMock<IForwarderHttpClientFactory>().Object,
            CreateStrictMock<IHttpContextAccessor>().Object,
            MockTokens.Object,
            CreateStrictMock<IDPoPProofFactory>().Object,
            BffTestFrontends.CreateCatalog()
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Services.Dispose();
        base.Dispose(disposing);
    }

    private BffTransformProvider CreateProvider()
    {
        var requirement = CreateStrictMock<IBffTokenRequirement>();
        requirement.SetupGet(x => x.Name).Returns(BffTokenRequirementNames.User).Verifiable();

        return new BffTransformProvider(
            ClientFactory,
            [requirement.Object],
            MockTokens.Object,
            Microsoft.Extensions.Options.Options.Create(
                new BffOptions
                {
                    Antiforgery = new BffAntiforgeryOptions { HeaderName = AntiforgeryHeaderName },
                }
            )
        );
    }

    private static RouteConfig CreateRoute(Dictionary<string, string>? metadata) =>
        new()
        {
            RouteId = "orders",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/api/orders/{**rest}" },
            Metadata = metadata,
        };

    #region Constructor Tests

    [Fact]
    public void Constructor_OtherClientFactory_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new BffTransformProvider(
                CreateStrictMock<IForwarderHttpClientFactory>().Object,
                [],
                MockTokens.Object,
                Microsoft.Extensions.Options.Options.Create(new BffOptions())
            )
        );

        Assert.StartsWith(
            "AddBffTransforms() must register the last IForwarderHttpClientFactory",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region ValidateRoute Tests

    [Fact]
    public void ValidateRoute_UnregisteredRequirement_AddsAnError()
    {
        var context = new TransformRouteValidationContext
        {
            Services = Services,
            Route = CreateRoute(
                new Dictionary<string, string>
                {
                    [BffReverseProxyMetadataKeys.TokenRequirement] = "unknown",
                }
            ),
        };

        CreateProvider().ValidateRoute(context);

        var error = Assert.Single(context.Errors);
        Assert.IsType<ArgumentException>(error);
    }

    [Fact]
    public void ValidateRoute_RegisteredRequirement_AddsNoError()
    {
        var context = new TransformRouteValidationContext
        {
            Services = Services,
            Route = CreateRoute(
                new Dictionary<string, string>
                {
                    [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.User,
                }
            ),
        };

        CreateProvider().ValidateRoute(context);

        Assert.Empty(context.Errors);
    }

    #endregion

    #region ValidateCluster Tests

    [Fact]
    public void ValidateCluster_AddsNoError()
    {
        var context = new TransformClusterValidationContext
        {
            Services = Services,
            Cluster = new ClusterConfig { ClusterId = "api" },
        };

        CreateProvider().ValidateCluster(context);

        Assert.Empty(context.Errors);
    }

    #endregion

    #region Apply Tests

    [Fact]
    public void Apply_TokenRoute_AddsTheTokenTransform()
    {
        var context = new TransformBuilderContext
        {
            Services = Services,
            Route = CreateRoute(
                new Dictionary<string, string>
                {
                    [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.User,
                    [BffReverseProxyMetadataKeys.Scope] = "orders.read",
                }
            ),
        };

        CreateProvider().Apply(context);

        var transform = Assert.IsType<BffTokenRequestTransform>(
            Assert.Single(context.RequestTransforms)
        );
        Assert.Equal(BffTokenRequirementNames.User, transform.TokenRequest.Requirement);
        Assert.Equal("orders.read", transform.TokenRequest.Scope);
        Assert.Equal(AntiforgeryHeaderName, transform.AntiforgeryHeaderName);
    }

    [Fact]
    public void Apply_OtherRoute_AddsNoTransform()
    {
        var context = new TransformBuilderContext
        {
            Services = Services,
            Route = CreateRoute(null),
        };

        CreateProvider().Apply(context);

        Assert.Empty(context.RequestTransforms);
        Assert.Empty(context.ResponseTransforms);
    }

    #endregion
}
