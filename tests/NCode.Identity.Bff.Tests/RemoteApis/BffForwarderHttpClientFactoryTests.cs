// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public sealed class BffForwarderHttpClientFactoryTests : BaseTests
{
    private Mock<IForwarderHttpClientFactory> MockInner { get; }

    private BffForwarderHttpClientFactory Factory { get; }

    private HttpRequestHeaders? SentHeaders { get; set; }

    private StubHttpMessageHandler Upstream { get; }

    public BffForwarderHttpClientFactoryTests()
    {
        MockInner = CreateStrictMock<IForwarderHttpClientFactory>();
        Upstream = new StubHttpMessageHandler(
            (request, _) =>
            {
                SentHeaders = request.Headers;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.TryAddWithoutValidation("Set-Cookie", "theme=dark");
                return Task.FromResult(response);
            }
        );
        var registrations = new BffClientRegistrationProvider(
            Microsoft.Extensions.Options.Options.Create(
                new BffOptions { Authority = "https://op.example.com", ClientId = "bff" }
            )
        );
        Factory = new BffForwarderHttpClientFactory(
            MockInner.Object,
            CreateStrictMock<IHttpContextAccessor>().Object,
            CreateStrictMock<IBffTokenAccessor>().Object,
            CreateStrictMock<IDPoPProofFactory>().Object,
            registrations
        );
    }

    private static ForwarderHttpClientContext CreateContext(HttpMessageInvoker? oldClient) =>
        new()
        {
            ClusterId = "api",
            OldConfig = new HttpClientConfig { MaxConnectionsPerServer = 1 },
            OldMetadata = new Dictionary<string, string> { ["Old"] = "1" },
            OldClient = oldClient,
            NewConfig = new HttpClientConfig { MaxConnectionsPerServer = 2 },
            NewMetadata = new Dictionary<string, string> { ["New"] = "2" },
        };

    private void SetupInner(
        ForwarderHttpClientContext context,
        HttpMessageInvoker? expectedOldClient,
        HttpMessageInvoker client
    ) =>
        MockInner
            .Setup(x =>
                x.CreateClient(
                    It.Is<ForwarderHttpClientContext>(inner =>
                        inner.ClusterId == context.ClusterId
                        && inner.OldConfig == context.OldConfig
                        && inner.OldMetadata == context.OldMetadata
                        && inner.OldClient == expectedOldClient
                        && inner.NewConfig == context.NewConfig
                        && inner.NewMetadata == context.NewMetadata
                    )
                )
            )
            .Returns(client)
            .Verifiable();

    private BffForwarderInvoker CreateWrapper(HttpMessageInvoker client)
    {
        var context = CreateContext(null);
        SetupInner(context, null, client);
        return Assert.IsType<BffForwarderInvoker>(Factory.CreateClient(context));
    }

    private static HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/orders");
        request.Headers.TryAddWithoutValidation("Cookie", "__Host-Http-bff=handle; theme=dark");
        return request;
    }

    #region CreateClient Tests

    [Fact]
    public void CreateClient_FirstClient_WrapsTheInnerClient()
    {
        using var client = new HttpMessageInvoker(Upstream);

        using var wrapper = CreateWrapper(client);

        Assert.Same(client, wrapper.Inner);
    }

    [Fact]
    public void CreateClient_InnerClientReused_ReturnsTheOldWrapper()
    {
        using var client = new HttpMessageInvoker(Upstream);
        using var oldWrapper = CreateWrapper(client);
        var context = CreateContext(oldWrapper);
        SetupInner(context, client, client);

        var result = Factory.CreateClient(context);

        Assert.Same(oldWrapper, result);
    }

    [Fact]
    public void CreateClient_NewInnerClient_ReturnsANewWrapper()
    {
        using var oldClient = new HttpMessageInvoker(Upstream);
        using var oldWrapper = CreateWrapper(oldClient);
        using var newClient = new HttpMessageInvoker(Upstream);
        var context = CreateContext(oldWrapper);
        SetupInner(context, oldClient, newClient);

        using var result = Factory.CreateClient(context);

        var wrapper = Assert.IsType<BffForwarderInvoker>(result);
        Assert.NotSame(oldWrapper, wrapper);
        Assert.Same(newClient, wrapper.Inner);
    }

    [Fact]
    public void CreateClient_ForeignOldClient_PassesItToTheInnerFactory()
    {
        using var foreign = new HttpMessageInvoker(Upstream);
        using var client = new HttpMessageInvoker(Upstream);
        var context = CreateContext(foreign);
        SetupInner(context, foreign, client);

        using var result = Factory.CreateClient(context);

        Assert.Same(client, Assert.IsType<BffForwarderInvoker>(result).Inner);
    }

    [Fact]
    public async Task CreateClient_ResolvedToken_AttachesItAndRemovesEveryCookie()
    {
        using var wrapper = CreateWrapper(new HttpMessageInvoker(Upstream));
        using var request = CreateRequest();
        request.Options.Set(
            BffAccessTokenHandler.ResolvedTokenKey,
            BffTokenResult.Success(
                new BffAccessToken(
                    "access",
                    OpenIdProtocol.TokenTypes.Bearer,
                    DateTimeOffset.MaxValue
                )
            )
        );

        using var response = await wrapper.SendAsync(request, CancellationToken.None);

        var recorded = Assert.Single(Upstream.Requests);
        Assert.Equal($"{OpenIdProtocol.TokenTypes.Bearer} access", recorded.Authorization);
        Assert.NotNull(SentHeaders);
        Assert.False(SentHeaders.Contains("Cookie"));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task CreateClient_NoResolvedToken_RemovesOnlyTheBffCookies()
    {
        using var wrapper = CreateWrapper(new HttpMessageInvoker(Upstream));
        using var request = CreateRequest();

        using var response = await wrapper.SendAsync(request, CancellationToken.None);

        var recorded = Assert.Single(Upstream.Requests);
        Assert.Null(recorded.Authorization);
        Assert.NotNull(SentHeaders);
        Assert.Equal("theme=dark", Assert.Single(SentHeaders.GetValues("Cookie")));
        Assert.Equal("theme=dark", Assert.Single(response.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public async Task CreateClient_WrapperDisposed_DisposesTheInnerClient()
    {
        var client = new HttpMessageInvoker(Upstream);
        var wrapper = CreateWrapper(client);

        wrapper.Dispose();

        using var request = CreateRequest();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            client.SendAsync(request, CancellationToken.None)
        );
    }

    [Fact]
    public void CreateClient_SynchronousSend_Throws()
    {
        using var wrapper = CreateWrapper(new HttpMessageInvoker(Upstream));
        using var request = CreateRequest();

        Assert.Throws<NotSupportedException>(() => wrapper.Send(request, CancellationToken.None));
        Assert.Empty(Upstream.Requests);
    }

    #endregion
}
