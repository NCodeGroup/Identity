// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
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

    private ClientSecretPostClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            CreateLogger<ClientSecretPostClientAuthenticationHandler>()
        );

    // A verifier that confirms one secret, as a stored one-way verifier does.
    private sealed class FakeVerifier(string keyId, string secret) : SecretVerifier
    {
        private byte[] Secret { get; } = SecureEncoding.UTF8.GetBytes(secret);

        public override KeyMetadata Metadata { get; } = new() { KeyId = keyId };

        public override string Scheme => SecretVerifierSchemes.Sha256;

        public override ValueTask<bool> VerifyAsync(
            ReadOnlyMemory<byte> secret,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(secret.Span.SequenceEqual(Secret));
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
        mockConfidentialClient.SetupGet(x => x.IsConfidential).Returns(true);
        mockConfidentialClient
            .SetupGet(x => x.ConfidentialClient)
            .Returns(mockConfidentialClient.Object);
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
            .Returns([new FakeVerifier("verifier-1", "another-secret")])
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
                new FakeVerifier("verifier-1", "another-secret"),
                new FakeVerifier("verifier-2", "presented-secret"),
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
            [new FakeVerifier("verifier-1", "another-secret")],
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
            [new FakeVerifier("verifier-1", "presented-secret-2")],
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

    #endregion
}
