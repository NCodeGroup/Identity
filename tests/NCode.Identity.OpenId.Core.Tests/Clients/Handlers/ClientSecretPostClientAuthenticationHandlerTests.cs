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

using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
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
