// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

public class ClientSecretPostClientAuthenticationHandlerTests : ClientAuthenticationHandlerTestBase
{
    protected override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost;

    #region Scaffolding

    // A verifier that confirms one secret, as a stored one-way verifier does, and counts its verifications. One with a
    // work factor that is created unavailable answers Unavailable, as one refused a turn does.
    private sealed class FakeSecretVerifier(
        string keyId,
        string secret,
        bool hasWorkFactor = false,
        bool isUnavailable = false
    ) : SecretVerifier
    {
        private byte[] Secret { get; } = SecureEncoding.UTF8.GetBytes(secret);

        private bool IsUnavailable { get; } = isUnavailable;

        public int VerifyCount { get; private set; }

        public override KeyMetadata Metadata { get; } = new() { KeyId = keyId };

        public override string Scheme =>
            HasWorkFactor ? SecretVerifierSchemes.Pbkdf2Sha256 : SecretVerifierSchemes.Sha256;

        public override bool HasWorkFactor { get; } = hasWorkFactor;

        public override ValueTask<SecretVerificationResult> VerifyAsync(
            ReadOnlyMemory<byte> secret,
            CancellationToken cancellationToken
        )
        {
            VerifyCount++;
            if (IsUnavailable)
            {
                return ValueTask.FromResult(SecretVerificationResult.Unavailable);
            }

            return ValueTask.FromResult(
                secret.Span.SequenceEqual(Secret)
                    ? SecretVerificationResult.Success
                    : SecretVerificationResult.Failed
            );
        }
    }

    private ClientSecretPostClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            MockAttemptLimiter.Object,
            CreateLogger<ClientSecretPostClientAuthenticationHandler>()
        );

    // The client's keys are never set up: a method that compares its secret authenticates only against the client's
    // verifiers, so the strict client mock proves the handler never reads them.
    private OpenIdClient CreateRegisteredClient(
        IReadOnlyCollection<SecretVerifier> verifiers,
        bool readsClientId
    )
    {
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            [OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost],
            readsClientId
        );
        Mock.Get(client).SetupGet(x => x.SecretVerifiers).Returns(verifiers).Verifiable();
        return client;
    }

    private OpenIdConfidentialClient SetupConfidentialClient(
        OpenIdClient client,
        string expectedCredentialId
    )
    {
        var mockConfidentialClient = CreateStrictMock<OpenIdConfidentialClient>();
        mockConfidentialClient.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();
        mockConfidentialClient
            .SetupGet(x => x.ConfidentialClient)
            .Returns(mockConfidentialClient.Object)
            .Verifiable();
        var confidentialClient = mockConfidentialClient.Object;
        MockClientFactory
            .Setup(x =>
                x.CreateConfidentialClientAsync(
                    MockContext.Object,
                    client,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                    expectedCredentialId,
                    null,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(confidentialClient)
            .Verifiable();
        return confidentialClient;
    }

    private void SetupCredentialsForm() =>
        SetupForm(
            new Dictionary<string, StringValues>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = "presented-secret",
            }
        );

    #endregion

    #region AuthenticateClientAsync Tests

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheFormHasNoClientSecret_ReturnsUndefined()
    {
        // A client_id alone is not a client_secret_post credential.
        SetupForm(new Dictionary<string, StringValues> { ["client_id"] = ClientId });
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsRegisteredForAnotherMethod_ReturnsInvalidClient()
    {
        // The secret is never compared: a client_secret_basic client does not authenticate with client_secret_post.
        SetupCredentialsForm();
        SetupErrorFactory();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            ceiling: null,
            readsClientId: true
        );
        SetupResolvedClient(client);
        SetupAudit("authentication_method_not_registered");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenThePublicClientPresentsASecret_ReturnsInvalidClient()
    {
        SetupCredentialsForm();
        SetupErrorFactory();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.None,
            ceiling: null,
            readsClientId: true
        );
        SetupResolvedClient(client);
        SetupAudit("authentication_method_not_registered");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheCeilingExcludesTheRegisteredMethod_ReturnsInvalidClient()
    {
        SetupCredentialsForm();
        SetupErrorFactory();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            [OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt],
            readsClientId: true
        );
        SetupResolvedClient(client);
        SetupAudit("authentication_method_not_permitted");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheRegisteredClientPresentsAnUnknownSecret_ReturnsInvalidClient()
    {
        SetupCredentialsForm();
        SetupErrorFactory();
        var client = CreateRegisteredClient(
            [new FakeSecretVerifier("verifier-1", "another-secret")],
            readsClientId: true
        );
        SetupResolvedClient(client);
        SetupAudit("invalid_client_secret");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheRegisteredClientPresentsAnUnknownSecret_CountsTheFailure()
    {
        SetupCredentialsForm();
        SetupErrorFactory();
        var client = CreateRegisteredClient(
            [new FakeSecretVerifier("verifier-1", "another-secret")],
            readsClientId: true
        );
        SetupResolvedClient(client);
        SetupAudit("invalid_client_secret");
        MockAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, ClientId, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var handler = CreateHandler();

        await handler.AuthenticateClientAsync(MockContext.Object, CancellationToken.None);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheAttemptLimitIsReached_RefusesBeforeResolvingTheClient()
    {
        // The strict resolver has no setup, so resolving the client or verifying a secret would fail the test.
        SetupCredentialsForm();
        SetupErrorFactory();
        MockAttemptLimiter
            .Setup(x => x.CheckAsync(MockContext.Object, ClientId, CancellationToken.None))
            .ReturnsAsync(new RateLimitDecision(TimeSpan.FromSeconds(90.5)))
            .Verifiable();
        SetupAudit("attempt_limit_reached");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Equal(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable, result.Error.Code);
        Assert.Equal(StatusCodes.Status429TooManyRequests, result.Error.StatusCode);
        var headers = result.Error.ResponseHeaders;
        Assert.NotNull(headers);
        Assert.Equal("91", headers[HeaderNames.RetryAfter].ToString());
        Assert.Equal("no-store", headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheSecretMatchesAVerifier_AuthenticatesWithThatSecret()
    {
        // A secret the server only compares is stored as a one-way verifier. See ADR-1391.
        SetupCredentialsForm();
        var client = CreateRegisteredClient(
            [
                new FakeSecretVerifier("verifier-1", "another-secret"),
                new FakeSecretVerifier("verifier-2", "presented-secret"),
            ],
            readsClientId: false
        );
        SetupResolvedClient(client);
        var expected = SetupConfidentialClient(client, "verifier-2");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsConfidential);
        Assert.Same(expected, result.ConfidentialClient);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheSecretIsAPrefixOfAStoredSecret_ReturnsInvalidClient()
    {
        SetupCredentialsForm();
        SetupErrorFactory();
        var client = CreateRegisteredClient(
            [new FakeSecretVerifier("verifier-1", "presented-secret-2")],
            readsClientId: true
        );
        SetupResolvedClient(client);
        SetupAudit("invalid_client_secret");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenAVerifierWithoutAWorkFactorMatches_SkipsTheCostlyVerifiers()
    {
        // The verifiers without a work factor run first, so a generated secret costs no derivation, whatever the storage
        // order. See ADR-1391.
        SetupCredentialsForm();
        var costly = new FakeSecretVerifier("verifier-1", "presented-secret", hasWorkFactor: true);
        var client = CreateRegisteredClient(
            [costly, new FakeSecretVerifier("verifier-2", "presented-secret")],
            readsClientId: false
        );
        SetupResolvedClient(client);
        var expected = SetupConfidentialClient(client, "verifier-2");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsConfidential);
        Assert.Same(expected, result.ConfidentialClient);
        Assert.Equal(0, costly.VerifyCount);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenOnlyACostlyVerifierMatches_AuthenticatesWithIt()
    {
        SetupCredentialsForm();
        var cheap = new FakeSecretVerifier("verifier-1", "another-secret");
        var client = CreateRegisteredClient(
            [new FakeSecretVerifier("verifier-2", "presented-secret", hasWorkFactor: true), cheap],
            readsClientId: false
        );
        SetupResolvedClient(client);
        var expected = SetupConfidentialClient(client, "verifier-2");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsConfidential);
        Assert.Same(expected, result.ConfidentialClient);
        Assert.Equal(1, cheap.VerifyCount);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenACostlyVerificationIsRefused_ReturnsServiceUnavailable()
    {
        // The gate refused the derivation, so the secret was never compared: the answer is 503, never invalid_client.
        SetupCredentialsForm();
        SetupErrorFactory();
        var client = CreateRegisteredClient(
            [
                new FakeSecretVerifier(
                    "verifier-1",
                    "presented-secret",
                    hasWorkFactor: true,
                    isUnavailable: true
                ),
            ],
            readsClientId: true
        );
        SetupResolvedClient(client);
        SetupAudit("secret_verification_unavailable");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Equal(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable, result.Error.Code);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.Error.StatusCode);
        var responseHeaders = result.Error.ResponseHeaders;
        Assert.NotNull(responseHeaders);
        Assert.Equal("1", responseHeaders[HeaderNames.RetryAfter]);
        Assert.Equal("no-store", responseHeaders[HeaderNames.CacheControl]);
    }

    #endregion
}
