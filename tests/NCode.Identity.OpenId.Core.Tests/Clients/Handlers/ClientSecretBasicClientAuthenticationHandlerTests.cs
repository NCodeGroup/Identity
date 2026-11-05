// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

public class ClientSecretBasicClientAuthenticationHandlerTests : ClientAuthenticationHandlerTestBase
{
    protected override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic;

    #region Scaffolding

    private ClientSecretBasicClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            MockAttemptLimiter.Object,
            CreateLogger<ClientSecretBasicClientAuthenticationHandler>()
        );

    private void SetupBasicCredentials(string credentials)
    {
        var encoded = Convert.ToBase64String(SecureEncoding.UTF8.GetBytes(credentials));
        HttpContext.Request.Method = HttpMethods.Post;
        HttpContext.Request.Headers.Authorization = $"Basic {encoded}";
    }

    // The 401 challenge names the tenant's issuer as its realm.
    private void SetupChallengeRealm()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.Issuer).Returns("https://issuer.example").Verifiable();
        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    #endregion

    #region AuthenticateClientAsync Tests

    [Fact]
    public async Task AuthenticateClientAsync_WhenThereIsNoAuthorizationHeader_ReturnsUndefined()
    {
        HttpContext.Request.Method = HttpMethods.Post;
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheCredentialsHaveNoSecret_Returns401InvalidClient()
    {
        // RFC 7617 credentials are 'user-id:password'; without the colon there is no client_secret to verify, so the
        // strict limiter proves the header is neither asked about nor counted.
        SetupBasicCredentials(ClientId);
        SetupErrorFactory();
        SetupChallengeRealm();
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsRegisteredForAnotherMethod_Returns401InvalidClient()
    {
        SetupBasicCredentials($"{ClientId}:presented-secret");
        SetupErrorFactory();
        SetupChallengeRealm();
        var client = CreateClient(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            ceiling: null,
            readsClientId: true
        );
        SetupAttemptAdmitted();
        SetupResolvedClient(client);
        SetupAudit("authentication_method_not_registered");
        SetupFailureCounted();
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status401Unauthorized);
    }

    #endregion
}
