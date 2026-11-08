// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.PrincipalResolution;
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
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        MockClientPrincipalResolver = CreateStrictMock<IClientPrincipalResolver>();

        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => CreatedErrorCode = code)
            .Returns(CreateLooseMock<IOpenIdError>().Object);

        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();

        MockContext = CreateStrictMock<OpenIdContext>();
        MockContext.SetupGet(x => x.Mediator).Returns(MockMediator.Object).Verifiable();
        MockContext.SetupGet(x => x.Environment).Returns(MockEnvironment.Object).Verifiable();
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        OpenIdContext = MockContext.Object;

        HttpContext = new DefaultHttpContext();
        HttpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == OpenIdContext)
        );

        Provider = new DefaultUserInfoEndpointProvider(
            TimeProvider.System,
            MockClientResolver.Object,
            MockCredentialSelector.Object,
            MockJwtEncoder.Object,
            MockAuditEventRecorder.Object,
            MockClientPrincipalResolver.Object
        );
    }

    private Mock<IMediator> MockMediator { get; }
    private Mock<IOpenIdClientResolver> MockClientResolver { get; }
    private Mock<IClientJwtCredentialSelector> MockCredentialSelector { get; }
    private Mock<IJwtEncoder> MockJwtEncoder { get; }
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private Mock<IClientPrincipalResolver> MockClientPrincipalResolver { get; }
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private Mock<OpenIdContext> MockContext { get; }
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

    // Every response reads whether the client's settings audit the access; only a signed response reads the client's
    // id and token lifetime and the tenant's issuer.
    private OpenIdClient SetupClient(bool signsResponse = false, bool auditsAccess = false)
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        SetupClientPrincipalResolver(null);
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.AuditUserInfoAccess))
            .Returns(auditsAccess)
            .Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        if (signsResponse)
        {
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.IdTokenLifetime))
                .Returns(TimeSpan.FromMinutes(5))
                .Verifiable();
            mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();

            var mockTenant = CreateStrictMock<OpenIdTenant>();
            mockTenant.SetupGet(x => x.Issuer).Returns(Issuer).Verifiable();
            MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        }

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

    // The resolver decides whether the authenticated subject is a client acting for itself.
    private void SetupClientPrincipalResolver(PersistedClient? actingClient) =>
        MockClientPrincipalResolver
            .Setup(x =>
                x.GetActingClientOrDefaultAsync(
                    OpenIdContext,
                    It.Is<ClaimsPrincipal>(user => user.FindFirstValue("sub") == "principal-1"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(actingClient)
            .Verifiable();

    private static PersistedClient CreatePersistedClient() =>
        new PersistedClientBuilder { ClientId = ClientId, PrincipalId = "principal-1" }.Build();

    // A client acting for itself is refused before its settings or id are read.
    private void SetupActingClient()
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        SetupClientPrincipalResolver(CreatePersistedClient());

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
    }

    // A refusal reads whether the tenant's settings audit the access, since no client's settings can be read.
    private void SetupTenantAudit(bool auditsAccess)
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.AuditUserInfoAccess))
            .Returns(auditsAccess)
            .Verifiable();
        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.SetupGet(x => x.Collection).Returns(mockSettings.Object).Verifiable();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.SettingsProvider).Returns(mockProvider.Object).Verifiable();
        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    private void SetupUnresolvedClient() =>
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

    // Only a JSON response reads the environment's serializer options.
    private void SetupJsonSerializerOptions() =>
        MockEnvironment
            .SetupGet(x => x.JsonSerializerOptions)
            .Returns(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Verifiable();

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
        SetupTenantAudit(auditsAccess: false);

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenClientIsNotEnabled_ReturnsInvalidToken()
    {
        SetupAuthenticatedSubject(ClientId);
        SetupUnresolvedClient();
        SetupTenantAudit(auditsAccess: false);

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenTheTokenIsTheClientActingForItself_ReturnsInvalidToken()
    {
        SetupAuthenticatedSubject(ClientId);
        SetupActingClient();
        SetupTenantAudit(auditsAccess: false);

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenClientRegistersNoProtection_ReturnsJson()
    {
        SetupAuthenticatedSubject(ClientId);
        var openIdClient = SetupClient();
        SetupClaims();
        SetupCredentials(openIdClient, new ClientJwtCredentials(null, null));
        SetupJsonSerializerOptions();

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<UserInfoResult>>(result);
        Assert.Equal("principal-1", json.Value?.Claims["sub"]);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenClientRegistersSigning_ReturnsJwtAddressedToTheClient()
    {
        SetupAuthenticatedSubject(ClientId);
        var openIdClient = SetupClient(signsResponse: true);
        SetupClaims();
        var signingCredentials = new JoseSigningCredentials(
            CreateLooseMock<SecretKey>().Object,
            CreateLooseMock<SignatureAlgorithm>().Object
        );
        SetupCredentials(openIdClient, new ClientJwtCredentials(signingCredentials, null));
        JwtEncodingParameters? encoded = null;
        MockJwtEncoder
            .Setup(x =>
                x.EncodeAsync(It.IsAny<JwtEncodingParameters>(), It.IsAny<CancellationToken>())
            )
            .Callback<JwtEncodingParameters, CancellationToken>(
                (parameters, _) => encoded = parameters
            )
            .ReturnsAsync("header.payload.signature")
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

    [Fact]
    public async Task HandleRouteAsync_WhenTheClientAuditsAccess_AuditsTheDisclosedClaimNames()
    {
        SetupAuthenticatedSubject(ClientId);
        var openIdClient = SetupClient(auditsAccess: true);
        SetupClaims();
        SetupCredentials(openIdClient, new ClientJwtCredentials(null, null));
        SetupJsonSerializerOptions();
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordUserInfoAccessedAsync(
                    OpenIdContext,
                    openIdClient,
                    "principal-1",
                    It.Is<IEnumerable<string>>(names => names.SequenceEqual(new[] { "sub" })),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenTheClientDoesNotAuditAccess_AuditsNothing()
    {
        // The strict recorder has no setup, so an audit would fail the test.
        SetupAuthenticatedSubject(ClientId);
        var openIdClient = SetupClient(auditsAccess: false);
        SetupClaims();
        SetupCredentials(openIdClient, new ClientJwtCredentials(null, null));
        SetupJsonSerializerOptions();

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.IsType<JsonHttpResult<UserInfoResult>>(result);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenTheTenantAuditsAccessAndTheClientIsNotEnabled_AuditsTheRefusal()
    {
        SetupAuthenticatedSubject(ClientId);
        SetupUnresolvedClient();
        SetupTenantAudit(auditsAccess: true);
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordUserInfoAccessDeniedAsync(
                    OpenIdContext,
                    "principal-1",
                    ClientId,
                    OpenIdProtocol.ErrorCodes.InvalidToken,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenTheTenantAuditsAccessAndTheClientActsForItself_AuditsTheRefusal()
    {
        SetupAuthenticatedSubject(ClientId);
        SetupActingClient();
        SetupTenantAudit(auditsAccess: true);
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordUserInfoAccessDeniedAsync(
                    OpenIdContext,
                    "principal-1",
                    ClientId,
                    OpenIdProtocol.ErrorCodes.InvalidToken,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenTheTenantAuditsAccessAndTheTokenNamesNoClient_AuditsTheRefusalWithoutAClient()
    {
        // The token authenticated but names no client, so the refusal is audited with no client id; the strict client
        // resolver proves no client is looked up. See ADR-1401.
        SetupAuthenticatedSubject(clientId: null);
        SetupTenantAudit(auditsAccess: true);
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordUserInfoAccessDeniedAsync(
                    OpenIdContext,
                    "principal-1",
                    null,
                    OpenIdProtocol.ErrorCodes.InvalidToken,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidToken, CreatedErrorCode);
    }

    #endregion
}
