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
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffAccessTokenHandlerTests : BaseTests
{
    private const string Jkt = "thumbprint";
    private const string Proof = "proof-jwt";
    private const string Nonce = "server-nonce";
    private static readonly Uri RequestUri = new("https://api.example.com/items");

    private readonly Mock<IHttpContextAccessor> _accessor;
    private readonly Mock<IBffTokenAccessor> _tokens;
    private readonly Mock<IDPoPProofFactory> _proofs;
    private readonly BffClientRegistrationProvider _registrations;
    private readonly BffTokenRequest _tokenRequest = new(BffTokenRequirementNames.User);

    public BffAccessTokenHandlerTests()
    {
        _accessor = CreateStrictMock<IHttpContextAccessor>();
        _tokens = CreateStrictMock<IBffTokenAccessor>();
        _proofs = CreateStrictMock<IDPoPProofFactory>();
        var bffOptions = new BffOptions
        {
            Authority = "https://op.example.com",
            ClientId = "bff",
            DPoP = new BffDPoPOptions { Mode = BffDPoPModes.Required, KeyRing = "dpop-keys" },
        };
        _registrations = new BffClientRegistrationProvider(
            Microsoft.Extensions.Options.Options.Create(bffOptions)
        );
    }

    private HttpClient CreateClient(StubHttpMessageHandler inner)
    {
        var handler = new BffAccessTokenHandler(
            _accessor.Object,
            _tokens.Object,
            _proofs.Object,
            _registrations,
            _tokenRequest
        )
        {
            InnerHandler = inner,
        };
        return new HttpClient(handler);
    }

    private void SetupResult(DefaultHttpContext httpContext, BffTokenResult result)
    {
        _accessor.SetupGet(x => x.HttpContext).Returns(httpContext).Verifiable();
        _tokens
            .Setup(x =>
                x.GetAccessTokenAsync(httpContext, _tokenRequest, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<BffTokenResult>(result))
            .Verifiable();
    }

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_NoHttpContext_Throws()
    {
        _accessor.SetupGet(x => x.HttpContext).Returns((HttpContext?)null).Verifiable();
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var client = CreateClient(inner);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(RequestUri));

        Assert.Empty(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_BearerToken_SetsBearerAuthorization()
    {
        SetupResult(
            new DefaultHttpContext(),
            BffTokenResult.Success(
                new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
            )
        );
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var client = CreateClient(inner);

        using var response = await client.GetAsync(RequestUri);

        var recorded = Assert.Single(inner.Requests);
        Assert.Equal("Bearer at", recorded.Authorization);
        Assert.Null(recorded.DPoP);
    }

    [Fact]
    public async Task SendAsync_BearerToken_ReplacesCallerHeaders()
    {
        SetupResult(
            new DefaultHttpContext(),
            BffTokenResult.Success(
                new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
            )
        );
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var client = CreateClient(inner);
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestUri);
        request.Headers.TryAddWithoutValidation("Authorization", "Basic abc");
        request.Headers.TryAddWithoutValidation(OpenIdProtocol.DPoP.HeaderName, "stale");

        using var response = await client.SendAsync(request);

        var recorded = Assert.Single(inner.Requests);
        Assert.Equal("Bearer at", recorded.Authorization);
        Assert.Null(recorded.DPoP);
    }

    [Fact]
    public async Task SendAsync_DPoPToken_SetsAuthorizationAndProof()
    {
        var httpContext = new DefaultHttpContext();
        SetupResult(
            httpContext,
            BffTokenResult.Success(
                new BffAccessToken("at", "dpop", DateTimeOffset.MaxValue)
                {
                    DPoPJwkThumbprint = Jkt,
                }
            )
        );
        _proofs
            .Setup(x =>
                x.CreateResourceProofAsync(
                    _registrations.Registration,
                    Jkt,
                    HttpMethod.Get,
                    RequestUri,
                    "at",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<string>(Proof))
            .Verifiable();
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var client = CreateClient(inner);

        using var response = await client.GetAsync(RequestUri);

        var recorded = Assert.Single(inner.Requests);
        Assert.Equal("DPoP at", recorded.Authorization);
        Assert.Equal(Proof, recorded.DPoP);
    }

    [Fact]
    public async Task SendAsync_DPoPTokenWithoutThumbprint_Throws()
    {
        SetupResult(
            new DefaultHttpContext(),
            BffTokenResult.Success(
                new BffAccessToken("at", OpenIdProtocol.TokenTypes.DPoP, DateTimeOffset.MaxValue)
            )
        );
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var client = CreateClient(inner);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(RequestUri));

        Assert.Empty(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_FailedResult_ThrowsWithStatus()
    {
        SetupResult(
            new DefaultHttpContext(),
            BffTokenResult.Failure(503, BffTokenFailureReasons.ProviderUnavailable)
        );
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var client = CreateClient(inner);

        var exception = await Assert.ThrowsAsync<BffTokenUnavailableException>(() =>
            client.GetAsync(RequestUri)
        );

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(BffTokenFailureReasons.ProviderUnavailable, exception.Reason);
        Assert.Empty(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_NullToken_SendsRequestUnchanged()
    {
        SetupResult(new DefaultHttpContext(), BffTokenResult.Success(null));
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var client = CreateClient(inner);

        using var response = await client.GetAsync(RequestUri);

        var recorded = Assert.Single(inner.Requests);
        Assert.Null(recorded.Authorization);
        Assert.Null(recorded.DPoP);
    }

    [Fact]
    public async Task SendAsync_ResponseWithNonce_RecordsNonce()
    {
        SetupResult(
            new DefaultHttpContext(),
            BffTokenResult.Success(
                new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
            )
        );
        _proofs.Setup(x => x.RecordNonce(RequestUri, Nonce)).Verifiable();
        var challenge = new HttpResponseMessage(HttpStatusCode.OK);
        challenge.Headers.Add(OpenIdProtocol.DPoP.NonceHeaderName, Nonce);
        var inner = StubHttpMessageHandler.Sequence(challenge);
        using var client = CreateClient(inner);

        using var response = await client.GetAsync(RequestUri);

        Assert.Single(inner.Requests);
    }

    private static HttpResponseMessage CreateChallenge(string scheme)
    {
        var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        challenge.Headers.Add(OpenIdProtocol.DPoP.NonceHeaderName, Nonce);
        challenge.Headers.TryAddWithoutValidation(
            "WWW-Authenticate",
            $"{scheme} error=\"{OpenIdProtocol.ErrorCodes.UseDPoPNonce}\""
        );
        return challenge;
    }

    private void SetupDPoPToken()
    {
        SetupResult(
            new DefaultHttpContext(),
            BffTokenResult.Success(
                new BffAccessToken("at", "dpop", DateTimeOffset.MaxValue)
                {
                    DPoPJwkThumbprint = Jkt,
                }
            )
        );
    }

    private void SetupProofs(params string[] proofValues)
    {
        var queue = new Queue<string>(proofValues);
        _proofs
            .Setup(x =>
                x.CreateResourceProofAsync(
                    _registrations.Registration,
                    Jkt,
                    HttpMethod.Post,
                    RequestUri,
                    "at",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => new ValueTask<string>(queue.Dequeue()))
            .Verifiable();
        _proofs.Setup(x => x.RecordNonce(RequestUri, Nonce)).Verifiable();
    }

    [Fact]
    public async Task SendAsync_DPoPNonceChallenge_RetriesOnceWithNewProof()
    {
        SetupDPoPToken();
        SetupProofs("proof-1", "proof-2");
        var inner = StubHttpMessageHandler.Sequence(
            CreateChallenge("DPoP"),
            new HttpResponseMessage(HttpStatusCode.OK)
        );
        using var client = CreateClient(inner);

        using var response = await client.PostAsync(
            RequestUri,
            new StringContent("body", SecureEncoding.UTF8)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal("proof-1", inner.Requests[0].DPoP);
        Assert.Equal("proof-2", inner.Requests[1].DPoP);
        Assert.Equal("body", inner.Requests[0].Body);
        Assert.Equal("body", inner.Requests[1].Body);
    }

    [Fact]
    public async Task SendAsync_DPoPNonceChallengeTwice_ReturnsSecondResponse()
    {
        SetupDPoPToken();
        SetupProofs("proof-1", "proof-2");
        var inner = StubHttpMessageHandler.Sequence(
            CreateChallenge("DPoP"),
            CreateChallenge("DPoP")
        );
        using var client = CreateClient(inner);

        using var response = await client.PostAsync(
            RequestUri,
            new StringContent("body", SecureEncoding.UTF8)
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_BearerChallenge_IsNotRetried()
    {
        SetupResult(
            new DefaultHttpContext(),
            BffTokenResult.Success(
                new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
            )
        );
        _proofs.Setup(x => x.RecordNonce(RequestUri, Nonce)).Verifiable();
        var inner = StubHttpMessageHandler.Sequence(CreateChallenge("Bearer"));
        using var client = CreateClient(inner);

        using var response = await client.GetAsync(RequestUri);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(inner.Requests);
    }

    #endregion
}
