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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Client.Discovery;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Discovery;

public class DefaultProviderMetadataResolverTests : BaseTests
{
    private const string Authority = "https://op.example.test";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    private MutableTimeProvider TimeProvider { get; } = new(DateTimeOffset.UnixEpoch);

    public DefaultProviderMetadataResolverTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private DefaultProviderMetadataResolver CreateResolver(HttpMessageHandler handler)
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
            .Returns(new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        return CreateResolverCore();
    }

    private DefaultProviderMetadataResolver CreateResolverCore()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions());
        return new DefaultProviderMetadataResolver(
            CreateLooseMock<ILogger<DefaultProviderMetadataResolver>>().Object,
            TimeProvider,
            options,
            new ProviderHttpClient(MockHttpClientFactory.Object, options)
        );
    }

    private static string CreateDocument(
        string issuer = Authority,
        bool includeJwksUri = true,
        string? revocationEndpoint = null
    ) =>
        $$"""
            {
              "issuer": "{{issuer}}",
              "authorization_endpoint": "{{issuer.TrimEnd('/')}}/authorize",
              "token_endpoint": "{{issuer.TrimEnd('/')}}/token"
              {{(includeJwksUri ? $$""", "jwks_uri": "{{issuer.TrimEnd('/')}}/jwks" """ : "")}}
              {{(
                revocationEndpoint is null
                    ? ""
                    : $$""", "revocation_endpoint": "{{revocationEndpoint}}" """
            )}}
            }
            """;

    #region GetMetadataAsync Tests

    [Fact]
    public async Task GetMetadataAsync_WithValidDocument_ReturnsMetadata()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, CreateDocument());
        var resolver = CreateResolver(handler);

        var metadata = await resolver.GetMetadataAsync(Authority, CancellationToken.None);

        Assert.Equal(Authority, metadata.Issuer);
        Assert.Equal($"{Authority}/token", metadata.TokenEndpoint);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://op.example.test/.well-known/openid-configuration",
            request.RequestUri?.AbsoluteUri
        );
        Assert.Equal("application/json", request.Accept);
    }

    [Fact]
    public async Task GetMetadataAsync_WithPathAuthority_FetchesUnderPath()
    {
        const string pathAuthority = "https://op.example.test/t1/";
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateDocument(pathAuthority)
        );
        var resolver = CreateResolver(handler);

        var metadata = await resolver.GetMetadataAsync(pathAuthority, CancellationToken.None);

        Assert.Equal(pathAuthority, metadata.Issuer);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://op.example.test/t1/.well-known/openid-configuration",
            request.RequestUri?.AbsoluteUri
        );
    }

    [Fact]
    public async Task GetMetadataAsync_WithIssuerMismatch_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateDocument("https://op.example.test/")
        );
        var resolver = CreateResolver(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetMetadataAsync(Authority, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetMetadataAsync_WithMissingJwksUri_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateDocument(includeJwksUri: false)
        );
        var resolver = CreateResolver(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetMetadataAsync(Authority, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetMetadataAsync_WithInsecureEndpoint_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateDocument(revocationEndpoint: "http://evil.test/r")
        );
        var resolver = CreateResolver(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetMetadataAsync(Authority, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetMetadataAsync_WithServerError_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.InternalServerError,
            """{"error":"boom"}"""
        );
        var resolver = CreateResolver(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetMetadataAsync(Authority, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetMetadataAsync_WhenTheProviderIsUnreachable_Throws()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => throw new HttpRequestException("unreachable")
        );
        var resolver = CreateResolver(handler);

        var exception = await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetMetadataAsync(Authority, CancellationToken.None)
        );

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Theory]
    [InlineData("[]", "application/json")]
    [InlineData("null", "application/json")]
    [InlineData("<html></html>", "text/html")]
    public async Task GetMetadataAsync_WithNonObjectResponse_Throws(string body, string mediaType)
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, body, mediaType);
        var resolver = CreateResolver(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetMetadataAsync(Authority, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetMetadataAsync_WithUndeserializableDocument_ThrowsWithJsonException()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"issuer":123}""");
        var resolver = CreateResolver(handler);

        var exception = await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await resolver.GetMetadataAsync(Authority, CancellationToken.None)
        );

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task GetMetadataAsync_WithSecondCallWithinInterval_UsesCache()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, CreateDocument());
        var resolver = CreateResolver(handler);

        var first = await resolver.GetMetadataAsync(Authority, CancellationToken.None);
        var second = await resolver.GetMetadataAsync(Authority, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetMetadataAsync_WithInsecureAuthority_ThrowsArgumentException()
    {
        var resolver = CreateResolverCore();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await resolver.GetMetadataAsync("http://op.example.test", CancellationToken.None)
        );
    }

    #endregion
}
