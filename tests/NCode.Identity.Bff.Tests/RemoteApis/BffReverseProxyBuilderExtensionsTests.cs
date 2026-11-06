// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public sealed class BffReverseProxyBuilderExtensionsTests : BaseTests
{
    private ServiceCollection ServiceCollection { get; } = new();

    private ServiceProvider? Provider { get; set; }

    public BffReverseProxyBuilderExtensionsTests()
    {
        ServiceCollection.AddLogging();
        ServiceCollection.AddHttpContextAccessor();
        ServiceCollection.AddOptions<BffOptions>();
        ServiceCollection.AddSingleton(CreateLooseMock<IBffTokenAccessor>().Object);
        ServiceCollection.AddSingleton(CreateLooseMock<IDPoPProofFactory>().Object);
        var frontends = BffTestFrontends.CreateCatalog();
        var timeProvider = new FakeTimeProvider();
        var cookies = new BffCookies(timeProvider);
        var resolver = new BffSessionResolver(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            [],
            frontends,
            timeProvider
        );
        ServiceCollection.AddSingleton<
            IBffConnectionSessionAccessor,
            DefaultBffConnectionSessionAccessor
        >();
        ServiceCollection.AddSingleton(new BffTokenContexts(frontends, resolver, cookies));
        ServiceCollection.AddSingleton(frontends);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Provider?.Dispose();
        base.Dispose(disposing);
    }

    private IServiceProvider Build()
    {
        Provider = ServiceCollection.BuildServiceProvider();
        return Provider;
    }

    private static ForwarderHttpClientContext CreateContext() =>
        new()
        {
            ClusterId = "api",
            OldConfig = new HttpClientConfig(),
            NewConfig = new HttpClientConfig(),
        };

    private static BffForwarderInvoker CreateClient(IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IForwarderHttpClientFactory>();
        Assert.IsType<BffForwarderHttpClientFactory>(factory);
        return Assert.IsType<BffForwarderInvoker>(factory.CreateClient(CreateContext()));
    }

    private Mock<IForwarderHttpClientFactory> SetupCustomFactory(HttpMessageInvoker client)
    {
        var custom = CreateStrictMock<IForwarderHttpClientFactory>();
        custom
            .Setup(x => x.CreateClient(It.IsAny<ForwarderHttpClientContext>()))
            .Returns(client)
            .Verifiable();
        return custom;
    }

    #region AddBffTransforms Tests

    [Fact]
    public void AddBffTransforms_DefaultFactory_DecoratesIt()
    {
        ServiceCollection.AddReverseProxy().AddBffTransforms();
        var provider = Build();

        using var client = CreateClient(provider);

        Assert.IsNotType<BffForwarderInvoker>(client.Inner);
        Assert.Contains(
            provider.GetServices<ITransformProvider>(),
            transformProvider => transformProvider is BffTransformProvider
        );
    }

    [Fact]
    public void AddBffTransforms_AfterConfigureHttpClient_KeepsTheCallback()
    {
        var isConfigured = false;
        ServiceCollection
            .AddReverseProxy()
            .ConfigureHttpClient((_, _) => isConfigured = true)
            .AddBffTransforms();
        var provider = Build();

        using var client = CreateClient(provider);

        Assert.True(isConfigured);
    }

    [Fact]
    public void AddBffTransforms_AfterACustomFactoryInstance_DecoratesIt()
    {
        using var inner = new HttpMessageInvoker(
            StubHttpMessageHandler.Respond(System.Net.HttpStatusCode.OK)
        );
        var custom = SetupCustomFactory(inner);
        var builder = ServiceCollection.AddReverseProxy();
        ServiceCollection.AddSingleton(custom.Object);
        builder.AddBffTransforms();

        using var client = CreateClient(Build());

        Assert.Same(inner, client.Inner);
    }

    [Fact]
    public void AddBffTransforms_AfterACustomFactoryDelegate_DecoratesIt()
    {
        using var inner = new HttpMessageInvoker(
            StubHttpMessageHandler.Respond(System.Net.HttpStatusCode.OK)
        );
        var custom = SetupCustomFactory(inner);
        var builder = ServiceCollection.AddReverseProxy();
        ServiceCollection.AddSingleton<IForwarderHttpClientFactory>(_ => custom.Object);
        builder.AddBffTransforms();

        using var client = CreateClient(Build());

        Assert.Same(inner, client.Inner);
    }

    [Fact]
    public void AddBffTransforms_CalledTwice_DecoratesOnce()
    {
        ServiceCollection.AddReverseProxy().AddBffTransforms().AddBffTransforms();
        var provider = Build();

        using var client = CreateClient(provider);

        Assert.IsNotType<BffForwarderInvoker>(client.Inner);
        Assert.Single(
            ServiceCollection,
            descriptor => descriptor.ImplementationType == typeof(BffTransformProvider)
        );
    }

    [Fact]
    public void AddBffTransforms_KeepsTheFactoryLifetime()
    {
        ServiceCollection.AddReverseProxy().AddBffTransforms();

        var descriptor = Assert.Single(
            ServiceCollection,
            descriptor => descriptor.ServiceType == typeof(IForwarderHttpClientFactory)
        );
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

    [Fact]
    public void AddBffTransforms_NoFactory_Throws()
    {
        var builder = CreateStrictMock<IReverseProxyBuilder>();
        builder.SetupGet(x => x.Services).Returns(new ServiceCollection()).Verifiable();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Object.AddBffTransforms()
        );

        Assert.Equal(
            "AddBffTransforms() needs the IForwarderHttpClientFactory that AddReverseProxy() registers.",
            exception.Message
        );
    }

    [Fact]
    public void AddBffTransforms_ThenConfigureHttpClient_Throws()
    {
        var builder = ServiceCollection.AddReverseProxy().AddBffTransforms();

        Assert.Throws<InvalidOperationException>(() => builder.ConfigureHttpClient((_, _) => { }));
    }

    [Fact]
    public void AddBffTransforms_ThenACustomFactory_RefusesToStart()
    {
        using var inner = new HttpMessageInvoker(
            StubHttpMessageHandler.Respond(System.Net.HttpStatusCode.OK)
        );
        ServiceCollection.AddReverseProxy().AddBffTransforms();
        ServiceCollection.AddSingleton(CreateLooseMock<IForwarderHttpClientFactory>().Object);
        var provider = Build();

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetServices<ITransformProvider>().ToList()
        );
    }

    #endregion
}
