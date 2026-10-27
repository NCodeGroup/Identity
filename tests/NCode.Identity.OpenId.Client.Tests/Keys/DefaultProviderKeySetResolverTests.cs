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

using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Keys;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Keys;

public class DefaultProviderKeySetResolverTests : BaseTests, IAsyncLifetime
{
    private const string Authority = "https://op.example.test";
    private const string JwksUri = "https://op.example.test/jwks";

    private ServiceProvider ServiceProvider { get; } = CreateServiceProvider();

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    private Mock<IProviderMetadataResolver> MockMetadataResolver { get; }

    private MutableTimeProvider TimeProvider { get; } = new(DateTimeOffset.UnixEpoch);

    public DefaultProviderKeySetResolverTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        MockMetadataResolver = CreateStrictMock<IProviderMetadataResolver>();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary();
        return serviceCollection.BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await ServiceProvider.DisposeAsync();

    private DefaultProviderKeySetResolver CreateResolver(HttpMessageHandler handler)
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
            .Returns(new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        var metadata = new OpenIdProviderMetadata
        {
            Issuer = Authority,
            TokenEndpoint = $"{Authority}/token",
            JwksUri = JwksUri,
        };
        MockMetadataResolver
            .Setup(x => x.GetMetadataAsync(Authority, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(metadata))
            .Verifiable();

        var options = Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions());
        return new DefaultProviderKeySetResolver(
            CreateLooseMock<ILogger<DefaultProviderKeySetResolver>>().Object,
            TimeProvider,
            options,
            new ProviderHttpClient(MockHttpClientFactory.Object, options),
            MockMetadataResolver.Object,
            new ProviderKeySetImporter(ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>()),
            ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>()
        );
    }

    private static string CreateKeySet(string kid = "k1", string use = "sig")
    {
        using var rsa = RSA.Create(2048);
        return TestJwks.Set(TestJwks.Rsa(rsa, kid, use));
    }

    #region GetSigningKeysAsync Tests

    [Fact]
    public async Task GetSigningKeysAsync_WithValidSet_ReturnsKeys()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, CreateKeySet());
        var resolver = CreateResolver(handler);

        var keys = await resolver.GetSigningKeysAsync(Authority, CancellationToken.None);

        Assert.True(keys.TryGetByKeyId("k1", out var key));
        Assert.Equal("k1", key.KeyId);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(JwksUri, request.RequestUri?.AbsoluteUri);
        Assert.Contains("application/jwk-set+json", request.Accept);
    }

    [Fact]
    public async Task GetSigningKeysAsync_WithSecondCall_SendsNoRequest()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, CreateKeySet());
        var resolver = CreateResolver(handler);

        var first = await resolver.GetSigningKeysAsync(Authority, CancellationToken.None);
        var second = await resolver.GetSigningKeysAsync(Authority, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetSigningKeysAsync_WithRefusedSet_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateKeySet(kid: "enc", use: "enc")
        );
        var resolver = CreateResolver(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetSigningKeysAsync(Authority, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetSigningKeysAsync_WithNotFound_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.NotFound, "{}");
        var resolver = CreateResolver(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetSigningKeysAsync(Authority, CancellationToken.None)
        );
    }

    #endregion

    #region RefreshSigningKeysAsync Tests

    [Fact]
    public async Task RefreshSigningKeysAsync_WithinMinimumInterval_SendsNoRequest()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, CreateKeySet());
        var resolver = CreateResolver(handler);
        await resolver.GetSigningKeysAsync(Authority, CancellationToken.None);

        await resolver.RefreshSigningKeysAsync(Authority, CancellationToken.None);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RefreshSigningKeysAsync_AfterMinimumInterval_SendsRequest()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, CreateKeySet());
        var resolver = CreateResolver(handler);
        await resolver.GetSigningKeysAsync(Authority, CancellationToken.None);
        TimeProvider.UtcNow = TimeProvider.UtcNow.AddMinutes(6);

        await resolver.RefreshSigningKeysAsync(Authority, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    #endregion
}
