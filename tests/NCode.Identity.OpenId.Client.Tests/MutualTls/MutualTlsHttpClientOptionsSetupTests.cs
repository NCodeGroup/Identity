// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Http;
using Moq;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.MutualTls;
using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.MutualTls;

public class MutualTlsHttpClientOptionsSetupTests : BaseTests
{
    private Mock<ClientCertificateProvider> MockProvider { get; }
    private MutualTlsHttpClientOptionsSetup Setup { get; }
    private TestMetrics Metrics { get; } = new();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Metrics.Dispose();
        base.Dispose(disposing);
    }

    public MutualTlsHttpClientOptionsSetupTests()
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        MockProvider = CreateStrictMock<ClientCertificateProvider>(
            mockKeyRingProvider.Object,
            TimeProvider.System
        );
        var clientOptions = Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions());
        var resiliencePipelines = new ProviderResiliencePipelines(
            clientOptions,
            Metrics.CreateProviderMetrics()
        );
        Setup = new MutualTlsHttpClientOptionsSetup(MockProvider.Object, resiliencePipelines);
    }

    private sealed class TestHttpMessageHandlerBuilder : HttpMessageHandlerBuilder
    {
        public override string? Name { get; set; }

        public override HttpMessageHandler PrimaryHandler { get; set; } = null!;

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = [];

        public override HttpMessageHandler Build() => PrimaryHandler;
    }

    #region Configure Tests

    [Fact]
    public void Configure_WithUnrelatedName_AddsNoAction()
    {
        var options = new HttpClientFactoryOptions();

        Setup.Configure("other", options);

        Assert.Empty(options.HttpMessageHandlerBuilderActions);
        Assert.Empty(options.HttpClientActions);
    }

    [Fact]
    public void Configure_WithDefaultOverload_AddsNoAction()
    {
        var options = new HttpClientFactoryOptions();

        Setup.Configure(options);

        Assert.Empty(options.HttpMessageHandlerBuilderActions);
    }

    [Fact]
    public void Configure_WithPrefixedName_SetsThePrimaryAndResilienceHandlers()
    {
        using var handler = new SocketsHttpHandler();
        MockProvider.Setup(x => x.CreatePrimaryHandler("ABC")).Returns(handler).Verifiable();
        var options = new HttpClientFactoryOptions();

        Setup.Configure(OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + "ABC", options);

        var action = Assert.Single(options.HttpMessageHandlerBuilderActions);
        var builder = new TestHttpMessageHandlerBuilder();
        action(builder);
        Assert.Same(handler, builder.PrimaryHandler);
        var additionalHandler = Assert.Single(builder.AdditionalHandlers);
        Assert.IsType<ProviderResilienceHandler>(additionalHandler);
    }

    [Fact]
    public void Configure_WithPrefixedName_DisablesTheHttpClientTimeout()
    {
        var options = new HttpClientFactoryOptions();

        Setup.Configure(OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + "ABC", options);

        var action = Assert.Single(options.HttpClientActions);
        using var client = new HttpClient();
        action(client);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    #endregion
}
