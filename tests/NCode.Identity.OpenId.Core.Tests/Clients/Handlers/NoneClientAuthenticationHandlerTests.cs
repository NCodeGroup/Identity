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

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

public class NoneClientAuthenticationHandlerTests : ClientAuthenticationHandlerTestBase
{
    protected override string AuthenticationMethod =>
        OpenIdConstants.ClientAuthenticationMethods.None;

    #region Scaffolding

    private NoneClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            CreateLogger<NoneClientAuthenticationHandler>()
        );

    private void SetupClientIdForm() =>
        SetupForm(new Dictionary<string, StringValues> { ["client_id"] = ClientId });

    #endregion

    #region AuthenticateClientAsync Tests

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheRequestIsNotAFormPost_ReturnsUndefined()
    {
        HttpContext.Request.Method = HttpMethods.Get;
        HttpContext.Request.QueryString = new QueryString($"?client_id={ClientId}");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheFormHasNoClientId_ReturnsUndefined()
    {
        SetupForm(new Dictionary<string, StringValues> { ["grant_type"] = "refresh_token" });
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Theory]
    [InlineData("client_secret")]
    [InlineData("client_assertion")]
    public async Task AuthenticateClientAsync_WhenTheFormCarriesACredential_ReturnsUndefined(
        string credentialParameter
    )
    {
        // The request carries another method's credential, which that method's handler authenticates.
        SetupForm(
            new Dictionary<string, StringValues>
            {
                ["client_id"] = ClientId,
                [credentialParameter] = "credential",
            }
        );
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsRegisteredAsPublic_ReturnsThePublicClient()
    {
        SetupClientIdForm();
        var client = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.None,
            [OpenIdConstants.ClientAuthenticationMethods.None],
            readsClientId: false
        );
        SetupResolvedClient(client);
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsPublic);
        Assert.Same(client, result.PublicClient);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsRegisteredForAnotherMethod_ReturnsUndefined()
    {
        // A confidential client that omits its credentials, or a mutual-TLS client whose credential is its
        // certificate, never authenticates as public.
        SetupClientIdForm();
        var client = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            ceiling: null,
            readsClientId: false
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
    public async Task AuthenticateClientAsync_WhenTheCeilingExcludesNone_ReturnsInvalidClient()
    {
        SetupClientIdForm();
        SetupErrorFactory();
        var client = CreateClient(
            OpenIdConstants.ClientAuthenticationMethods.None,
            [OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt],
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
    public async Task AuthenticateClientAsync_WhenTheClientIsUnknown_ReturnsInvalidClient()
    {
        SetupClientIdForm();
        SetupErrorFactory();
        SetupTenant();
        SetupResolvedClient(null);
        SetupAudit("client_not_found");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheStoredCredentialsCannotBeRead_ReturnsInvalidClient()
    {
        SetupClientIdForm();
        SetupErrorFactory();
        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(MockContext.Object, ClientId, CancellationToken.None)
            )
            .ThrowsAsync(new CryptographicException("The key is not available."))
            .Verifiable();
        SetupAudit("credential_deserialization_failed");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    #endregion
}
