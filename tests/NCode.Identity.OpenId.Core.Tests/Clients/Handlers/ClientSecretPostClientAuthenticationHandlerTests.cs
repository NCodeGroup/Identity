// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.Secrets.Collections;
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

    // A shared secret the client holds as a key, as a client issued a secret for client_secret_jwt does.
    private sealed class FakeSymmetricSecretKey(string keyId, string secret) : SymmetricSecretKey
    {
        private byte[] Secret { get; } = SecureEncoding.UTF8.GetBytes(secret);

        public override KeyMetadata Metadata { get; } = new() { KeyId = keyId };

        public override int KeySizeBits => Secret.Length * 8;

        public override void ExportPrivateKey<TWriter>(ref TWriter destination)
        {
            Secret.CopyTo(destination.GetSpan(Secret.Length));
            destination.Advance(Secret.Length);
        }
    }

    private ClientSecretPostClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            CreateLogger<ClientSecretPostClientAuthenticationHandler>()
        );

    // The secret keys are set up only for a test whose presented secret matches no verifier, so it reaches them; a
    // test that passes none proves through the strict client mock that a matching verifier ends the search.
    private OpenIdClient CreateRegisteredClient(
        IReadOnlyCollection<SecretVerifier> verifiers,
        IReadOnlyCollection<SecretKey>? secretKeys,
        bool readsClientId
    )
    {
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            [OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost],
            readsClientId
        );
        Mock.Get(client).SetupGet(x => x.SecretVerifiers).Returns(verifiers).Verifiable();
        if (secretKeys is not null)
        {
            var mockSecretKeys = CreateStrictMock<ISecretKeyCollection>();
            mockSecretKeys
                .As<IEnumerable>()
                .Setup(x => x.GetEnumerator())
                .Returns(() => secretKeys.GetEnumerator())
                .Verifiable();
            Mock.Get(client)
                .SetupGet(x => x.SecretKeys)
                .Returns(mockSecretKeys.Object)
                .Verifiable();
        }

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
        var mockSecretKeys = CreateStrictMock<ISecretKeyCollection>();
        mockSecretKeys
            .As<IEnumerable>()
            .Setup(x => x.GetEnumerator())
            .Returns(() => Array.Empty<SecretKey>().GetEnumerator())
            .Verifiable();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            [OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost],
            readsClientId: true
        );
        Mock.Get(client).SetupGet(x => x.SecretKeys).Returns(mockSecretKeys.Object).Verifiable();
        Mock.Get(client)
            .SetupGet(x => x.SecretVerifiers)
            .Returns([new FakeSecretVerifier("verifier-1", "another-secret")])
            .Verifiable();
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
    public async Task AuthenticateClientAsync_WhenTheSecretMatchesAVerifier_AuthenticatesWithThatSecret()
    {
        // A secret the server only compares is stored as a one-way verifier. See ADR-1391.
        SetupCredentialsForm();
        var client = CreateRegisteredClient(
            [
                new FakeSecretVerifier("verifier-1", "another-secret"),
                new FakeSecretVerifier("verifier-2", "presented-secret"),
            ],
            secretKeys: null,
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
    public async Task AuthenticateClientAsync_WhenTheSecretMatchesASecretHeldAsAKey_AuthenticatesWithThatSecret()
    {
        // A secret issued while the client was registered for client_secret_jwt is held as a key, and still
        // authenticates the client once it is registered for a method that compares the secret.
        SetupCredentialsForm();
        var client = CreateRegisteredClient(
            [new FakeSecretVerifier("verifier-1", "another-secret")],
            [
                new FakeSymmetricSecretKey("key-1", "presented-secret-but-longer"),
                new FakeSymmetricSecretKey("key-2", "presented-secret"),
            ],
            readsClientId: false
        );
        SetupResolvedClient(client);
        var expected = SetupConfidentialClient(client, "key-2");
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
            [new FakeSymmetricSecretKey("key-1", "presented-secret-2")],
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
    public async Task AuthenticateClientAsync_WhenASecretHeldAsAKeyMatches_SkipsTheCostlyVerifiers()
    {
        // The cheap comparisons run first, so a matching key costs no derivation, whatever the storage order. See
        // ADR-1391.
        SetupCredentialsForm();
        var costly = new FakeSecretVerifier("verifier-1", "presented-secret", hasWorkFactor: true);
        var client = CreateRegisteredClient(
            [costly, new FakeSecretVerifier("verifier-2", "another-secret")],
            [new FakeSymmetricSecretKey("key-1", "presented-secret")],
            readsClientId: false
        );
        SetupResolvedClient(client);
        var expected = SetupConfidentialClient(client, "key-1");
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
            [new FakeSymmetricSecretKey("key-1", "another-secret")],
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
            [],
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
