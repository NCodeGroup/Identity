// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffGrantClientTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private Mock<ITokenClient> MockTokenClient { get; }
    private BffGrantClient Client { get; }

    private BffFrontend Frontend { get; } = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());

    public BffGrantClientTests()
    {
        MockTokenClient = CreateStrictMock<ITokenClient>();
        Client = new BffGrantClient(
            MockTokenClient.Object,
            new FakeTimeProvider(Now),
            CreateLooseMock<ILogger<BffGrantClient>>().Object
        );
    }

    private void SetupResult(TokenRequest request, TokenResult result) =>
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(It.IsAny<ClientRegistration>(), request, CancellationToken.None)
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupThrows(TokenRequest request, Exception exception) =>
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(It.IsAny<ClientRegistration>(), request, CancellationToken.None)
            )
            .ThrowsAsync(exception)
            .Verifiable();

    #region RequestAsync Tests

    [Fact]
    public async Task RequestAsync_Issued_ReturnsTokenExpiringAfterExpiresIn()
    {
        var request = new ClientCredentialsTokenRequest();
        SetupResult(
            request,
            new TokenResult(
                new TokenResponse
                {
                    AccessToken = "access",
                    TokenType = "DPoP",
                    ExpiresIn = 300,
                }
            )
            {
                DPoPJwkThumbprint = "jkt-1",
            }
        );

        var result = await Client.RequestAsync(Frontend, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal("access", token.Value);
        Assert.Equal("DPoP", token.TokenType);
        Assert.Equal(Now.AddSeconds(300), token.ExpiresAt);
        Assert.Equal("jkt-1", token.DPoPJwkThumbprint);
    }

    [Fact]
    public async Task RequestAsync_NoExpiresInOrTokenType_ReturnsBearerTokenExpiringNow()
    {
        var request = new ClientCredentialsTokenRequest();
        SetupResult(request, new TokenResult(new TokenResponse { AccessToken = "access" }));

        var result = await Client.RequestAsync(Frontend, request, CancellationToken.None);

        var token = Assert.IsType<BffAccessToken>(result.Token);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, token.TokenType);
        Assert.Equal(Now, token.ExpiresAt);
        Assert.Null(token.DPoPJwkThumbprint);
    }

    [Fact]
    public async Task RequestAsync_ProviderRefuses_ReturnsProviderError()
    {
        var request = new ClientCredentialsTokenRequest();
        SetupResult(request, new TokenResult(new ProtocolError("invalid_client", 401)));

        var result = await Client.RequestAsync(Frontend, request, CancellationToken.None);

        Assert.Equal(502, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderError, result.Reason);
    }

    [Fact]
    public async Task RequestAsync_NoAccessToken_ReturnsProviderError()
    {
        var request = new ClientCredentialsTokenRequest();
        SetupResult(request, new TokenResult(new TokenResponse { TokenType = "Bearer" }));

        var result = await Client.RequestAsync(Frontend, request, CancellationToken.None);

        Assert.Equal(502, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderError, result.Reason);
    }

    [Fact]
    public async Task RequestAsync_ProviderUnreachable_ReturnsProviderUnavailable()
    {
        var request = new ClientCredentialsTokenRequest();
        SetupThrows(request, new HttpRequestException("down"));

        var result = await Client.RequestAsync(Frontend, request, CancellationToken.None);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderUnavailable, result.Reason);
    }

    [Fact]
    public async Task RequestAsync_UnexpectedException_Throws()
    {
        var request = new ClientCredentialsTokenRequest();
        SetupThrows(request, new InvalidOperationException("bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Client.RequestAsync(Frontend, request, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task RequestAsync_WithFrontend_UsesItsRegistration()
    {
        var frontend = BffTestFrontends.Get(
            BffTestFrontends.CreateCatalog(x => x.ClientId = "frontend-client")
        );
        var request = new ClientCredentialsTokenRequest();
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.Is<ClientRegistration>(r => r.ClientId == "frontend-client"),
                    request,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new TokenResult(new TokenResponse { AccessToken = "access" }))
            .Verifiable();

        var result = await Client.RequestAsync(frontend, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RequestAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Client.RequestAsync(Frontend, null!, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
