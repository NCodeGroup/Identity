// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

public class SelfSignedTlsClientAuthClientAuthenticationHandlerTests
    : ClientAuthenticationHandlerTestBase
{
    protected override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth;

    #region Scaffolding

    private Mock<IClientCertificateResolver> MockClientCertificateResolver { get; }

    public SelfSignedTlsClientAuthClientAuthenticationHandlerTests()
    {
        MockClientCertificateResolver = CreateStrictMock<IClientCertificateResolver>();
    }

    // The key resolver is strict and never set up: no test reaches a presented certificate.
    private SelfSignedTlsClientAuthClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            MockAttemptLimiter.Object,
            CreateLogger<SelfSignedTlsClientAuthClientAuthenticationHandler>(),
            MockClientCertificateResolver.Object,
            CreateStrictMock<IClientKeyResolver>().Object
        );

    private void SetupClientIdForm() =>
        SetupForm(new Dictionary<string, StringValues> { ["client_id"] = ClientId });

    #endregion

    #region AuthenticateClientAsync Tests

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheAttemptLimitIsReached_RefusesBeforeResolvingTheCertificate()
    {
        // The limit is asked once the registration says the request is this method's, so the refusal is audited with
        // self_signed_tls_client_auth; the strict certificate resolver proves no certificate is verified. See ADR-1399.
        SetupClientIdForm();
        SetupErrorFactory();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
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

    #endregion
}
