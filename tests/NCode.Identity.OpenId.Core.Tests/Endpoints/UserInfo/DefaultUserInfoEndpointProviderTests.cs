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

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.UserInfo;

public class DefaultUserInfoEndpointProviderTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string Issuer = "https://op.example";

    public DefaultUserInfoEndpointProviderTests()
    {
        MockMediator = CreateStrictMock<IMediator>();
        MockClientResolver = CreateStrictMock<IOpenIdClientResolver>();
        MockCredentialSelector = CreateStrictMock<IClientJwtCredentialSelector>();
        MockJwtEncoder = CreateStrictMock<IJwtEncoder>();

        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        mockEnvironment
            .SetupGet(x => x.JsonSerializerOptions)
            .Returns(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => CreatedErrorCode = code)
            .Returns(CreateLooseMock<IOpenIdError>().Object);

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer);

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Mediator).Returns(MockMediator.Object);
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object);
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        OpenIdContext = mockContext.Object;

        HttpContext = new DefaultHttpContext();
        HttpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == OpenIdContext)
        );

        Provider = new DefaultUserInfoEndpointProvider(
            TimeProvider.System,
            MockClientResolver.Object,
            MockCredentialSelector.Object,
            MockJwtEncoder.Object
        );
    }

    private Mock<IMediator> MockMediator { get; }
    private Mock<IOpenIdClientResolver> MockClientResolver { get; }
    private Mock<IClientJwtCredentialSelector> MockCredentialSelector { get; }
    private Mock<IJwtEncoder> MockJwtEncoder { get; }
    private OpenIdContext OpenIdContext { get; }
    private DefaultHttpContext HttpContext { get; }
    private DefaultUserInfoEndpointProvider Provider { get; }
    private string? CreatedErrorCode { get; set; }

    #region Scaffolding

    private void SetupAuthenticatedSubject(string? clientId)
    {
        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim("sub", "principal-1"));
        if (clientId is not null)
            identity.AddClaim(new Claim("client_id", clientId));

        var ticket = new SubjectAuthentication(
            "Bearer",
            new AuthenticationProperties(),
            new ClaimsPrincipal(identity),
            "principal-1"
        );

        MockMediator
            .Setup(x =>
                x.SendAsync<AuthenticateResourceRequestCommand, AuthenticateSubjectDisposition>(
                    It.Is<AuthenticateResourceRequestCommand>(command =>
                        command.RequiredAudiences.Contains(
                            OpenIdConstants.SystemResourceServerIdentifiers.OpenId
                        )
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new AuthenticateSubjectDisposition(ticket))
            .Verifiable();
    }

    private OpenIdClient SetupClient()
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.IdTokenLifetime))
            .Returns(TimeSpan.FromMinutes(5));

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId);
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);

        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(
                    OpenIdContext,
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mockClient.Object)
            .Verifiable();

        return mockClient.Object;
    }

    private void SetupClaims() =>
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<GetUserInfoClaimsCommand>(), It.IsAny<CancellationToken>())
            )
            .Callback<GetUserInfoClaimsCommand, CancellationToken>(
                (command, _) => command.Claims["sub"] = "principal-1"
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupCredentials(OpenIdClient openIdClient, ClientJwtCredentials credentials) =>
        MockCredentialSelector
            .Setup(x =>
                x.SelectCredentialsAsync(
                    OpenIdContext,
                    openIdClient,
                    ClientJwtProtectionSettings.UserInfo,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(credentials)
            .Verifiable();

    #endregion

    #region HandleRouteAsync Tests

    [Fact]
    public async Task HandleRouteAsync_WhenAccessTokenNamesNoClient_ReturnsInvalidToken()
    {
        SetupAuthenticatedSubject(clientId: null);

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenClientIsNotEnabled_ReturnsInvalidToken()
    {
        SetupAuthenticatedSubject(ClientId);
        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(
                    OpenIdContext,
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((OpenIdClient?)null)
            .Verifiable();

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenClientRegistersNoProtection_ReturnsJson()
    {
        SetupAuthenticatedSubject(ClientId);
        var openIdClient = SetupClient();
        SetupClaims();
        SetupCredentials(openIdClient, new ClientJwtCredentials(null, null));

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<UserInfoResult>>(result);
        Assert.Equal("principal-1", json.Value?.Claims["sub"]);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenClientRegistersSigning_ReturnsJwtAddressedToTheClient()
    {
        SetupAuthenticatedSubject(ClientId);
        var openIdClient = SetupClient();
        SetupClaims();
        var signingCredentials = new JoseSigningCredentials(
            CreateLooseMock<SecretKey>().Object,
            CreateLooseMock<SignatureAlgorithm>().Object
        );
        SetupCredentials(openIdClient, new ClientJwtCredentials(signingCredentials, null));
        JwtEncodingParameters? encoded = null;
        MockJwtEncoder
            .Setup(x => x.Encode(It.IsAny<JwtEncodingParameters>()))
            .Callback<JwtEncodingParameters>(parameters => encoded = parameters)
            .Returns("header.payload.signature")
            .Verifiable();

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        var content = Assert.IsType<Utf8ContentHttpResult>(result);
        Assert.Equal("application/jwt", content.ContentType);
        Assert.Equal(
            "header.payload.signature",
            SecureEncoding.UTF8.GetString(content.ResponseContent.Span)
        );
        Assert.NotNull(encoded);
        Assert.Same(signingCredentials, encoded.SigningCredentials);
        Assert.Equal(Issuer, encoded.Issuer);
        Assert.Equal(ClientId, encoded.Audience.ToString());
        Assert.NotNull(encoded.ExtraPayloadClaims);
        Assert.Equal("principal-1", encoded.ExtraPayloadClaims["sub"]);
    }

    #endregion
}
