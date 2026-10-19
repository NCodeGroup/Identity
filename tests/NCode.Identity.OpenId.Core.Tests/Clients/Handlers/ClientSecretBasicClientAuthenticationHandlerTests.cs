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

using System.Text;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

public class ClientSecretBasicClientAuthenticationHandlerTests : ClientAuthenticationHandlerTestBase
{
    #region Scaffolding

    private ClientSecretBasicClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            CreateLogger<ClientSecretBasicClientAuthenticationHandler>()
        );

    private void SetupBasicCredentials(string credentials)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
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
        // RFC 7617 credentials are 'user-id:password'; without the colon there is no client_secret to verify.
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
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
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

        AssertInvalidClient(result, StatusCodes.Status401Unauthorized);
    }

    #endregion
}
