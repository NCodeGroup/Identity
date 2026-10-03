#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Globalization;
using System.Security.Cryptography;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.Handlers;

public class DefaultGetIdTokenPayloadClaimsHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private DefaultGetIdTokenPayloadClaimsHandler Handler { get; } = new();

    #region Scaffolding

    private (
        GetIdTokenPayloadClaimsCommand command,
        Dictionary<string, object> payloadClaims
    ) CreateCommand(CreateSecurityTokenRequest tokenRequest)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSigAlg = CreateStrictMock<SignatureAlgorithm>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        mockSigAlg
            .SetupGet(x => x.HashAlgorithmName)
            .Returns(HashAlgorithmName.SHA256)
            .Verifiable();

        var signingCredentials = new JoseSigningCredentials(SecretKey: null!, mockSigAlg.Object);
        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            signingCredentials,
            EncryptionCredentials: null,
            TokenType: OpenIdConstants.TokenTypes.IdToken
        );

        var payloadClaims = new Dictionary<string, object>(StringComparer.Ordinal);
        var command = new GetIdTokenPayloadClaimsCommand(
            mockContext.Object,
            mockClient.Object,
            tokenContext,
            payloadClaims
        );

        return (command, payloadClaims);
    }

    private static CreateSecurityTokenRequest CreateTokenRequest(
        string? nonce = null,
        string? authorizationCode = null,
        string? accessToken = null,
        string? state = null
    )
    {
        IReadOnlyList<string> scopes = ["openid"];
        return new CreateSecurityTokenRequest
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdConstants.GrantTypes.AuthorizationCode,
            OriginalScopes = scopes,
            EffectiveScopes = scopes,
            Nonce = nonce,
            AuthorizationCode = authorizationCode,
            AccessToken = accessToken,
            State = state,
        };
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_Always_PopulatesTidClientIdAzp()
    {
        var (command, payloadClaims) = CreateCommand(CreateTokenRequest());

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(TenantId, payloadClaims[JoseClaimNames.Payload.Tid]);
        Assert.Equal(ClientId, payloadClaims[JoseClaimNames.Payload.ClientId]);
        Assert.Equal(ClientId, payloadClaims[JoseClaimNames.Payload.Azp]);
        Assert.False(payloadClaims.ContainsKey(JoseClaimNames.Payload.Nonce));
        Assert.False(payloadClaims.ContainsKey(JoseClaimNames.Payload.CHash));
    }

    [Fact]
    public async Task HandleAsync_WhenNoncePresent_SetsNonce()
    {
        var (command, payloadClaims) = CreateCommand(CreateTokenRequest(nonce: "nonce-value"));

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("nonce-value", payloadClaims[JoseClaimNames.Payload.Nonce]);
    }

    [Fact]
    public async Task HandleAsync_WhenCodeTokenState_SetsParameterHashes()
    {
        var (command, payloadClaims) = CreateCommand(
            CreateTokenRequest(
                authorizationCode: "auth-code",
                accessToken: "access-token",
                state: "state-value"
            )
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(payloadClaims.ContainsKey(JoseClaimNames.Payload.CHash));
        Assert.True(payloadClaims.ContainsKey(JoseClaimNames.Payload.AtHash));
        Assert.True(payloadClaims.ContainsKey(JoseClaimNames.Payload.SHash));
    }

    #endregion
}
