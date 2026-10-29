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
using System.Net.Http.Headers;
using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Http;

public class ProviderHttpClientTests : BaseTests
{
    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    public ProviderHttpClientTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private ProviderHttpClient CreateClient(
        HttpMessageHandler handler,
        OpenIdClientOptions? options = null
    )
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
            .Returns(new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        return new ProviderHttpClient(
            MockHttpClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(options ?? new OpenIdClientOptions())
        );
    }

    private static HttpRequestMessage CreateRequest() =>
        new(HttpMethod.Get, "https://op.example.test/.well-known/openid-configuration");

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithJsonBody_ParsesJson()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"issuer":"x"}""");
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Json);
        Assert.Equal("x", response.Json.Value.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task SendAsync_WithHttpClientName_UsesThatClient()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"issuer":"x"}""");
        MockHttpClientFactory
            .Setup(x => x.CreateClient("custom"))
            .Returns(new HttpClient(handler, disposeHandler: false))
            .Verifiable();
        var client = new ProviderHttpClient(
            MockHttpClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions())
        );

        var response = await client.SendAsync(CreateRequest(), "custom", CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Json);
    }

    [Fact]
    public async Task SendAsync_WithHtmlBody_ReturnsNullJson()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.BadGateway,
            "<html></html>",
            "text/html"
        );
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(502, response.StatusCode);
        Assert.Null(response.Json);
    }

    [Fact]
    public async Task SendAsync_WithEmptyJsonBody_ReturnsNullJson()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Json);
    }

    [Fact]
    public async Task SendAsync_WithInvalidJson_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{not json");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(() =>
            client.SendAsync(CreateRequest(), CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task SendAsync_WithBodyOverCap_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, new string('a', 2048));
        var client = CreateClient(handler, new OpenIdClientOptions { MaxResponseSizeBytes = 1024 });

        await Assert.ThrowsAsync<OpenIdProviderException>(() =>
            client.SendAsync(CreateRequest(), CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task SendAsync_WithHttpRequestException_ThrowsWithInnerException()
    {
        var inner = new HttpRequestException("boom");
        var handler = new StubHttpMessageHandler((_, _) => throw inner);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<OpenIdProviderException>(() =>
            client.SendAsync(CreateRequest(), CancellationToken.None).AsTask()
        );

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public async Task SendAsync_WithTimeout_Throws()
    {
        var handler = new StubHttpMessageHandler(
            async (_, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new HttpResponseMessage();
            }
        );
        var client = CreateClient(
            handler,
            new OpenIdClientOptions { RequestTimeout = TimeSpan.FromMilliseconds(100) }
        );

        await Assert.ThrowsAsync<OpenIdProviderException>(() =>
            client.SendAsync(CreateRequest(), CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task SendAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var client = new ProviderHttpClient(
            MockHttpClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions())
        );
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SendAsync(CreateRequest(), cancellationSource.Token).AsTask()
        );
    }

    [Fact]
    public async Task SendAsync_WithDPoPNonceHeader_ReturnsNonce()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(
                            """{"error":"use_dpop_nonce"}""",
                            SecureEncoding.UTF8,
                            "application/json"
                        ),
                        Headers = { { "DPoP-Nonce", "n-1" } },
                    }
                )
        );
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("n-1", response.DPoPNonce);
    }

    [Fact]
    public async Task SendAsync_WithChallengeAndJwtBody_ReturnsTheChallengeAndMediaType()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("a.b.c", SecureEncoding.UTF8, "application/jwt"),
                };
                response.Headers.TryAddWithoutValidation(
                    "WWW-Authenticate",
                    "Bearer error=\"invalid_token\""
                );
                return Task.FromResult(response);
            }
        );
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(401, response.StatusCode);
        Assert.Null(response.Json);
        Assert.Equal("application/jwt", response.MediaType);
        Assert.Equal("Bearer error=\"invalid_token\"", response.WwwAuthenticate);
    }

    [Fact]
    public async Task SendAsync_WithJwtBody_ReturnsTheCompactJwt()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "a.b.c\r\n",
                            SecureEncoding.UTF8,
                            "application/jwt"
                        ),
                    }
                )
        );
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Json);
        Assert.Equal("a.b.c", response.Jwt);
    }

    [Fact]
    public async Task SendAsync_WhenAJwtResponseIsNotUtf8_ThrowsOpenIdProviderException()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) =>
            {
                var content = new ByteArrayContent([0x61, 0xFF, 0x62]);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/jwt");
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
                );
            }
        );
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<OpenIdProviderException>(() =>
            client.SendAsync(CreateRequest(), CancellationToken.None).AsTask()
        );

        Assert.Contains("not valid UTF-8", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithJsonBody_ReturnsNoJwt()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", SecureEncoding.UTF8, "application/json"),
                    }
                )
        );
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.NotNull(response.Json);
        Assert.Null(response.Jwt);
    }

    [Fact]
    public async Task SendAsync_WithoutDPoPNonceHeader_ReturnsNullNonce()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"issuer":"x"}""");
        var client = CreateClient(handler);

        var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Null(response.DPoPNonce);
    }

    #endregion
}
