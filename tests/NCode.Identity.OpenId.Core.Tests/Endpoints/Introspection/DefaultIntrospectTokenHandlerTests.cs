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

using System.Globalization;
using System.Text.Json;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Introspection;

public class DefaultIntrospectTokenHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string OtherClientId = "client-2";
    private const string Token = "token-value";
    private const string Audience = "https://api.example.com";
    private const string OtherAudience = "https://other.example.com";

    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private Mock<IIssuedTokenValidator> MockIssuedTokenValidator { get; }
    private Mock<ISubjectIdentifierService> MockSubjectIdentifierService { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdClient> MockClient { get; }
    private ISecretKeyCollection ResourceServerKeys { get; }
    private DefaultIntrospectTokenHandler Handler { get; }

    public DefaultIntrospectTokenHandlerTests()
    {
        MockIssuedTokenValidator = CreateStrictMock<IIssuedTokenValidator>();
        MockSubjectIdentifierService = CreateStrictMock<ISubjectIdentifierService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockClient = CreateStrictMock<OpenIdClient>();
        ResourceServerKeys = CreateLooseMock<ISecretKeyCollection>().Object;
        Handler = new DefaultIntrospectTokenHandler(
            MockIssuedTokenValidator.Object,
            MockSubjectIdentifierService.Object,
            MockPersistedGrantService.Object,
            MockTimeProvider.Object
        );
    }

    #region Scaffolding

    private IntrospectTokenCommand CreateCommand(
        string? token,
        IntrospectionResult result,
        ResolvedResourceServer? resourceServer = null
    )
    {
        var mockRequest = CreateStrictMock<ITokenIntrospectionRequest>();
        mockRequest.SetupGet(x => x.Token).Returns(token).Verifiable();

        return new IntrospectTokenCommand(
            MockContext.Object,
            MockClient.Object,
            resourceServer,
            mockRequest.Object,
            result
        );
    }

    // A command whose request is never read.
    private IntrospectTokenCommand CreateUnreadCommand(IntrospectionResult result) =>
        new(
            MockContext.Object,
            MockClient.Object,
            CreateResourceServer(),
            CreateStrictMock<ITokenIntrospectionRequest>().Object,
            result
        );

    private ResolvedResourceServer CreateResourceServer()
    {
        var mockSecretsProvider = CreateLooseMock<ISecretKeyCollectionProvider>();
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(ResourceServerKeys);

        return new ResolvedResourceServer
        {
            ResourceServerId = "rs-1",
            Identifier = Audience,
            Scopes = ["read:orders", "write:orders"],
            IsSystem = false,
            EffectiveSettings = CreateLooseMock<IReadOnlySettingCollection>().Object,
            SecretsProvider = mockSecretsProvider.Object,
        };
    }

    private void SetupAccessTokenValidation(object? payload) =>
        MockIssuedTokenValidator
            .Setup(x =>
                x.ValidateAccessTokenAsync(
                    MockContext.Object,
                    Token,
                    It.Is<AccessTokenValidationOptions>(options =>
                        options.RequiredAudiences.SequenceEqual(new[] { Audience })
                        && ReferenceEquals(options.AdditionalDecryptionKeys, ResourceServerKeys)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                payload is null
                    ? TestJwtValidationResults.Invalid(Token)
                    : TestJwtValidationResults.Valid(payload)
            )
            .Verifiable();

    private void SetupRefreshTokenLookup(PersistedGrant<RefreshTokenGrant>? grant)
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();

        var grantId = new PersistedGrantId
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.RefreshToken,
            GrantKey = Token,
        };

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.RefreshToken, Token)
            )
            .Returns(grantId)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<RefreshTokenGrant>(
                    MockContext.Object,
                    grantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(grant)
            .Verifiable();
    }

    private static PersistedGrant<RefreshTokenGrant> CreateGrant(
        PersistedGrantStatus status,
        string clientId,
        string? subjectId,
        IReadOnlyList<string> effectiveScopes,
        DateTimeOffset? lineageCreatedWhen = null
    ) =>
        new()
        {
            Status = status,
            TenantId = TenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            SessionId = null,
            Payload = new RefreshTokenGrant(
                clientId,
                [],
                effectiveScopes,
                null,
                default,
                lineageCreatedWhen ?? UtcNow
            ),
        };

    // The clock and the client's refresh token lifetime, which together decide whether a family is past its horizon.
    private void SetupRefreshTokenHorizon()
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenLifetime))
            .Returns(RefreshTokenLifetime)
            .Verifiable();

        MockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
    }

    private static string? ClaimString(IntrospectionResult result, string name) =>
        result.Claims.TryGetValue(name, out var value)
            ? value switch
            {
                string text => text,
                JsonElement element => element.GetString(),
                _ => value.ToString(),
            }
            : null;

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAlreadyActive_ReturnsWithoutChecking()
    {
        var result = new IntrospectionResult { Active = true };
        var command = CreateUnreadCommand(result);

        // Strict mocks ensure no validation or grant lookup occurs.
        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenMissing_LeavesInactive()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(token: null, result, CreateResourceServer());

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAccessTokenNamesTheCallersResourceServer_ReportsItNarrowedToTheCaller()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result, CreateResourceServer());
        SetupAccessTokenValidation(
            new Dictionary<string, object>
            {
                [JoseClaimNames.Payload.Sub] = "subject-1",
                [JoseClaimNames.Payload.Aud] = new[] { Audience, OtherAudience },
                [JoseClaimNames.Payload.Scope] = "openid read:orders profile write:orders",
            }
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.Active);
        Assert.Equal("subject-1", ClaimString(result, JoseClaimNames.Payload.Sub));
        Assert.Equal(Audience, ClaimString(result, JoseClaimNames.Payload.Aud));
        Assert.Equal("read:orders write:orders", ClaimString(result, JoseClaimNames.Payload.Scope));
    }

    [Fact]
    public async Task HandleAsync_WhenTheAccessTokenCarriesNoScopeOfTheCallersResourceServer_OmitsTheScope()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result, CreateResourceServer());
        SetupAccessTokenValidation(
            new Dictionary<string, object>
            {
                [JoseClaimNames.Payload.Aud] = Audience,
                [JoseClaimNames.Payload.Scope] = "openid profile",
            }
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.Active);
        Assert.False(result.Claims.ContainsKey(JoseClaimNames.Payload.Scope));
    }

    [Fact]
    public async Task HandleAsync_WhenTheTokenIsNotAnAccessTokenForTheCaller_FallsThroughToRefreshTokens()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result, CreateResourceServer());
        SetupAccessTokenValidation(payload: null);
        SetupRefreshTokenLookup(grant: null);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerActsForNoResourceServer_NeverReportsAnAccessToken()
    {
        // The strict validator mock has no setup, so an access-token validation would fail the test.
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        SetupRefreshTokenLookup(grant: null);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenTheRefreshTokenIsRevoked_LeavesInactive()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        SetupRefreshTokenLookup(
            CreateGrant(PersistedGrantStatus.Revoked, ClientId, subjectId: null, [])
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerOwnsAnActiveRefreshToken_SetsActiveAndProjectsTheSubject()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        MockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        SetupRefreshTokenHorizon();
        SetupRefreshTokenLookup(
            CreateGrant(PersistedGrantStatus.Active, ClientId, "subject-1", ["api", "profile"])
        );
        MockSubjectIdentifierService
            .Setup(x => x.ProjectSubjectId(MockContext.Object, MockClient.Object, "subject-1"))
            .Returns("pairwise-1")
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.Active);
        Assert.Equal(ClientId, result.Claims[JoseClaimNames.Payload.ClientId]);
        Assert.Equal("api profile", result.Claims[JoseClaimNames.Payload.Scope]);
        Assert.Equal("pairwise-1", result.Claims[JoseClaimNames.Payload.Sub]);
    }

    [Fact]
    public async Task HandleAsync_RefreshTokenPastItsFamilyHorizon_IsInactive()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        MockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        SetupRefreshTokenHorizon();
        SetupRefreshTokenLookup(
            CreateGrant(
                PersistedGrantStatus.Active,
                ClientId,
                "subject-1",
                ["api"],
                UtcNow - TimeSpan.FromDays(31)
            )
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public async Task HandleAsync_WhenAnotherClientOwnsTheRefreshToken_LeavesInactive()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        MockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        SetupRefreshTokenLookup(
            CreateGrant(PersistedGrantStatus.Active, OtherClientId, "subject-1", ["api"])
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
        Assert.Empty(result.Claims);
    }

    #endregion
}
