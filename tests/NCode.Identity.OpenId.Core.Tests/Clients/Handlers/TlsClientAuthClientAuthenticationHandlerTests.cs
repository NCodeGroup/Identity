// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.Secrets.KeyMaterial;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

public class TlsClientAuthClientAuthenticationHandlerTests : ClientAuthenticationHandlerTestBase
{
    protected override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth;

    #region Scaffolding

    private Mock<IClientCertificateResolver> MockClientCertificateResolver { get; }

    public TlsClientAuthClientAuthenticationHandlerTests()
    {
        MockClientCertificateResolver = CreateStrictMock<IClientCertificateResolver>();
    }

    // The chain validator and the key factory are strict and never set up: no test reaches a presented certificate.
    private TlsClientAuthClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            MockAttemptLimiter.Object,
            CreateLogger<TlsClientAuthClientAuthenticationHandler>(),
            MockClientCertificateResolver.Object,
            CreateStrictMock<IClientCertificateChainValidator>().Object,
            CreateStrictMock<ISecretKeyFactory>().Object
        );

    private void SetupClientIdForm() =>
        SetupForm(new Dictionary<string, StringValues> { ["client_id"] = ClientId });

    private void SetupNoCertificatePresented() =>
        MockClientCertificateResolver
            .Setup(x => x.ResolveCertificateAsync(MockContext.Object, CancellationToken.None))
            .ReturnsAsync((PresentedClientCertificate?)null)
            .Verifiable();

    #endregion

    #region AuthenticateClientAsync Tests

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsUnknown_ReturnsUndefined()
    {
        // The request carries only a client_id, as a public client's does, so until the registration says otherwise it
        // is not this handler's: the strict limiter proves it is neither asked about nor counted. See ADR-1399.
        SetupClientIdForm();
        SetupResolvedClient(null);
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsRegisteredForAnotherMethod_ReturnsUndefined()
    {
        SetupClientIdForm();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.None,
            ceiling: null,
            readsClientId: false,
            readsIsConfidential: false
        );
        SetupResolvedClient(client);
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheAttemptLimitIsReached_RefusesBeforeResolvingTheCertificate()
    {
        // The limit is asked once the registration says the request is this method's, so the refusal is audited with
        // tls_client_auth; the strict certificate resolver proves no certificate is verified.
        SetupClientIdForm();
        SetupErrorFactory();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth,
            ceiling: null,
            readsClientId: false,
            readsIsConfidential: false
        );
        SetupResolvedClient(client);
        SetupAttemptRefused();
        SetupAudit("attempt_limit_reached");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertAttemptLimitRefusal(result);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenNoCertificateIsPresented_CountsTheFailure()
    {
        SetupClientIdForm();
        SetupErrorFactory();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth,
            [OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth],
            readsClientId: false,
            readsIsConfidential: false
        );
        SetupResolvedClient(client);
        SetupAttemptAdmitted();
        SetupNoCertificatePresented();
        SetupFailureCounted();
        SetupAudit("client_certificate_missing");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    #endregion
}
