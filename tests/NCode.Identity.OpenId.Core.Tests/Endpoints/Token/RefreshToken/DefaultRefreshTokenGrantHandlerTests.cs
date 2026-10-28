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
using Moq;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.RefreshToken;

public class DefaultRefreshTokenGrantHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string RefreshTokenValue = "refresh-token";

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private DefaultRefreshTokenGrantHandler Handler { get; }

    public DefaultRefreshTokenGrantHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockTokenService = CreateStrictMock<ITokenService>();
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        Handler = new DefaultRefreshTokenGrantHandler(
            MockTimeProvider.Object,
            MockPersistedGrantService.Object,
            MockTokenService.Object,
            MockAuditEventRecorder.Object
        );
    }

    #region Scaffolding

    private static PersistedGrantId CreateGrantId() =>
        new()
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.RefreshToken,
            GrantKey = RefreshTokenValue,
        };

    private static SecurityToken CreateSecurityToken(string tokenValue) =>
        new()
        {
            TokenType = OpenIdProtocol.TokenTypes.Bearer,
            TokenValue = tokenValue,
            TokenLifetime = new TimePeriod
            {
                StartTime = CreatedWhen,
                EndTime = CreatedWhen.AddHours(1),
            },
        };

    private static PersistedGrant<RefreshTokenGrant> CreateActiveGrant(
        IReadOnlyList<string> scopes
    ) =>
        new()
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            Payload = new RefreshTokenGrant("client-1", scopes, scopes, null, default),
        };

    private static PersistedGrant<RefreshTokenGrant> CreateActiveGrantInLineage(
        IReadOnlyList<string> scopes
    ) => CreateActiveGrant(scopes) with { RefreshTokenLineageId = "lineage-1" };

    // A grant that a rotation spent: revoked and consumed when the successor was stored.
    private static PersistedGrant<RefreshTokenGrant> CreateRotatedGrant(
        IReadOnlyList<string> scopes,
        DateTimeOffset consumedWhen
    ) =>
        CreateActiveGrantInLineage(scopes) with
        {
            Status = PersistedGrantStatus.Revoked,
            ExpiresWhen = CreatedWhen.AddDays(1),
            ConsumedWhen = consumedWhen,
        };

    private static void SetupReuseInterval(
        Mock<IReadOnlySettingCollection> mockSettings,
        TimeSpan interval
    ) =>
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenReuseInterval))
            .Returns(interval)
            .Verifiable();

    private (
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IMediator> mediator,
        Mock<ITokenRequest> tokenRequest,
        Mock<IReadOnlySettingCollection> settings,
        Mock<IOpenIdError> error
    ) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        // Tenant, ErrorFactory, Mediator and Settings are all read unconditionally at the top of every path.
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        return (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, mockError);
    }

    private void SetupLookup(
        Mock<ITokenRequest> mockTokenRequest,
        PersistedGrant<RefreshTokenGrant>? persistedGrant
    )
    {
        mockTokenRequest.SetupGet(x => x.RefreshToken).Returns(RefreshTokenValue).Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(CreatedWhen).Verifiable();

        MockPersistedGrantService
            .Setup(x => x.CreateGrantId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(CreateGrantId())
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<RefreshTokenGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<RefreshTokenGrant>?>(persistedGrant))
            .Verifiable();
    }

    private static void SetupValidation(Mock<IMediator> mockMediator) =>
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateTokenGrantCommand<RefreshTokenGrant>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupTokenResponse(
        Mock<OpenIdContext> mockContext,
        Mock<OpenIdClient> mockClient,
        Mock<IMediator> mockMediator
    )
    {
        mockContext
            .SetupGet(x => x.Environment)
            .Returns(CreateStrictMock<OpenIdEnvironment>().Object)
            .Verifiable();

        SetupValidation(mockMediator);

        MockTokenService
            .Setup(x =>
                x.CreateAccessTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("access-token-value")))
            .Verifiable();
    }

    // The answer to a reuse: the replay audit and, under the default reuse policy, the revocation of the lineage.
    private void SetupReuseAnswer(
        Mock<OpenIdContext> mockContext,
        Mock<OpenIdClient> mockClient,
        Mock<IReadOnlySettingCollection> mockSettings
    )
    {
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenReusePolicy))
            .Returns(OpenIdConstants.RefreshTokenReusePolicy.RevokeAll)
            .Verifiable();

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordRefreshTokenReplayAsync(
                    mockContext.Object,
                    mockClient.Object,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.RevokeByRefreshTokenLineageAsync(
                    mockContext.Object,
                    "lineage-1",
                    CreatedWhen,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<long>(2L))
            .Verifiable();
    }

    #endregion

    #region GrantTypes Property Tests

    [Fact]
    public void GrantTypes_Always_ContainsRefreshToken()
    {
        Assert.Contains(OpenIdProtocol.GrantTypes.RefreshToken, Handler.GrantTypes);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenRefreshTokenMissing_ReturnsMissingParameterError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, _, mockError) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.RefreshToken).Returns((string?)null).Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantNotFound_ReturnsInvalidGrantError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, _, mockError) = CreateScaffold();

        SetupLookup(mockTokenRequest, null);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenBoundGrantAndProofMismatch_ReturnsError()
    {
        // RFC 9449 §5: a DPoP-bound refresh token presented without a matching proof is rejected.
        var (mockContext, mockClient, _, mockTokenRequest, _, mockError) = CreateScaffold();

        var boundGrant = new PersistedGrant<RefreshTokenGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            Payload = new RefreshTokenGrant(
                "client-1",
                ["api"],
                ["api"],
                null,
                new TokenConfirmation { JwkThumbprint = "jkt-1" }
            ),
        };
        SetupLookup(mockTokenRequest, boundGrant);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantNotActive_ReturnsInvalidGrantError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, _, mockError) = CreateScaffold();

        var revokedGrant = new PersistedGrant<RefreshTokenGrant>
        {
            Status = PersistedGrantStatus.Revoked,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            Payload = new RefreshTokenGrant("client-1", ["api"], ["api"], null, default),
        };
        SetupLookup(mockTokenRequest, revokedGrant);

        // A revoked refresh token presented again is a reuse/replay signal and is audited.
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordRefreshTokenReplayAsync(
                    mockContext.Object,
                    mockClient.Object,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantExpired_ReturnsInvalidGrantErrorWithoutReplayAudit()
    {
        var (mockContext, mockClient, _, mockTokenRequest, _, mockError) = CreateScaffold();

        var expiredGrant = new PersistedGrant<RefreshTokenGrant>
        {
            Status = PersistedGrantStatus.Expired,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            Payload = new RefreshTokenGrant("client-1", ["api"], ["api"], null, default),
        };
        SetupLookup(mockTokenRequest, expiredGrant);

        // The strict recorder mock has no setup: an expired (benign) token must not be audited as a replay.
        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantActive_ReturnsTokenResponseWithAccessToken()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, _, _) = CreateScaffold();

        SetupLookup(mockTokenRequest, CreateActiveGrant(["api"]));
        SetupTokenResponse(mockContext, mockClient, mockMediator);
        mockClient.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("access-token-value", response.AccessToken);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, response.TokenType);
        Assert.Equal(TimeSpan.FromHours(1), response.ExpiresIn);
        Assert.Equal(["api"], response.Scopes);
    }

    [Fact]
    public async Task HandleAsync_WhenRevokedAndReusePolicyRevokeAll_RevokesTokenFamily()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        var revokedGrant = new PersistedGrant<RefreshTokenGrant>
        {
            Status = PersistedGrantStatus.Revoked,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            RefreshTokenLineageId = "lineage-1",
            Payload = new RefreshTokenGrant("client-1", ["api"], ["api"], null, default),
        };
        SetupLookup(mockTokenRequest, revokedGrant);

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenReusePolicy))
            .Returns(OpenIdConstants.RefreshTokenReusePolicy.RevokeAll)
            .Verifiable();

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordRefreshTokenReplayAsync(
                    mockContext.Object,
                    mockClient.Object,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.RevokeByRefreshTokenLineageAsync(
                    mockContext.Object,
                    "lineage-1",
                    CreatedWhen,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<long>(2L))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenRevokedAndReusePolicyNone_DoesNotRevokeTokenFamily()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        var revokedGrant = new PersistedGrant<RefreshTokenGrant>
        {
            Status = PersistedGrantStatus.Revoked,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            RefreshTokenLineageId = "lineage-1",
            Payload = new RefreshTokenGrant("client-1", ["api"], ["api"], null, default),
        };
        SetupLookup(mockTokenRequest, revokedGrant);

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenReusePolicy))
            .Returns(OpenIdConstants.RefreshTokenReusePolicy.None)
            .Verifiable();

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordRefreshTokenReplayAsync(
                    mockContext.Object,
                    mockClient.Object,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        // The strict grant-service mock has no setup for lineage revocation: policy 'none' must not revoke the family.
        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenThePolicyIsAlways_ReturnsTheSuccessorRefreshToken()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, _) =
            CreateScaffold();

        SetupLookup(mockTokenRequest, CreateActiveGrantInLineage(["api", "offline_access"]));
        SetupTokenResponse(mockContext, mockClient, mockMediator);

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Always)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.RotateRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    CreateGrantId(),
                    It.Is<CreateSecurityTokenRequest>(request =>
                        request.RefreshTokenLineageId == "lineage-1"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>(CreateSecurityToken("successor-refresh-token")))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("successor-refresh-token", response.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WhenAConcurrentRequestRotatedTheTokenFirst_AnswersAsReuseAndRevokesTheLineage()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        // Minting is not set up on the strict mocks: a lost spend must mint nothing.
        SetupLookup(mockTokenRequest, CreateActiveGrantInLineage(["api", "offline_access"]));
        SetupValidation(mockMediator);

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Always)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.RotateRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    CreateGrantId(),
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>((SecurityToken?)null))
            .Verifiable();

        SetupReuseAnswer(mockContext, mockClient, mockSettings);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenThePolicyIsAlwaysWithoutOfflineAccess_RevokesThePresentedToken()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, _) =
            CreateScaffold();

        SetupLookup(mockTokenRequest, CreateActiveGrant(["api"]));
        SetupTokenResponse(mockContext, mockClient, mockMediator);

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Always)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.TryRevokeOnceAsync(
                    mockContext.Object,
                    CreateGrantId(),
                    CreatedWhen,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<bool>(true))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Null(response.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WhenTheSpendWithoutASuccessorIsLost_AnswersAsReuse()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        // Minting is not set up on the strict mocks: a lost spend must mint nothing.
        SetupLookup(mockTokenRequest, CreateActiveGrantInLineage(["api"]));
        SetupValidation(mockMediator);

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Always)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.TryRevokeOnceAsync(
                    mockContext.Object,
                    CreateGrantId(),
                    CreatedWhen,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<bool>(false))
            .Verifiable();

        SetupReuseAnswer(mockContext, mockClient, mockSettings);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenAPublicClientPresentsAnUnboundToken_RotatesIt()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, _) =
            CreateScaffold();

        SetupLookup(mockTokenRequest, CreateActiveGrantInLineage(["api", "offline_access"]));
        SetupTokenResponse(mockContext, mockClient, mockMediator);
        mockClient.SetupGet(x => x.IsConfidential).Returns(false).Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Unbound)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.RotateRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    CreateGrantId(),
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>(CreateSecurityToken("successor-refresh-token")))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("successor-refresh-token", response.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WhenAPublicClientPresentsADPoPBoundToken_KeepsIt()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, _) =
            CreateScaffold();

        // Rotation is not set up on the strict mocks: a sender-constrained token is not rotated.
        var scopes = new[] { "api", "offline_access" };
        var boundGrant = CreateActiveGrantInLineage(scopes) with
        {
            Payload = new RefreshTokenGrant(
                "client-1",
                scopes,
                scopes,
                null,
                new TokenConfirmation { JwkThumbprint = "jkt-1" }
            ),
        };
        SetupLookup(mockTokenRequest, boundGrant);
        SetupTokenResponse(mockContext, mockClient, mockMediator);
        mockClient.SetupGet(x => x.IsConfidential).Returns(false).Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Unbound)
            .Verifiable();

        var binding = new ProofOfPossessionBinding
        {
            TokenType = OpenIdProtocol.TokenTypes.DPoP,
            Confirmation = new TokenConfirmation { JwkThumbprint = "jkt-1" },
        };

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            binding,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal(RefreshTokenValue, response.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WhenAConfidentialClientPresentsAnUnboundToken_KeepsIt()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, _) =
            CreateScaffold();

        // Rotation is not set up on the strict mocks: a confidential client's token is not rotated.
        SetupLookup(mockTokenRequest, CreateActiveGrantInLineage(["api", "offline_access"]));
        SetupTokenResponse(mockContext, mockClient, mockMediator);
        mockClient.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Unbound)
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal(RefreshTokenValue, response.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WhenARotatedTokenIsPresentedWithinTheReuseInterval_IssuesAnotherSuccessor()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, _) =
            CreateScaffold();

        // The strict mocks have no setup for the replay audit, the rotation or the spend: a retry is none of them. The
        // retry audit is the only audit.
        SetupLookup(
            mockTokenRequest,
            CreateRotatedGrant(["api", "offline_access"], CreatedWhen.AddSeconds(-10))
        );
        SetupTokenResponse(mockContext, mockClient, mockMediator);
        SetupReuseInterval(mockSettings, TimeSpan.FromSeconds(30));

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.ReissueRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.Is<CreateSecurityTokenRequest>(request =>
                        request.RefreshTokenLineageId == "lineage-1"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>(CreateSecurityToken("another-refresh-token")))
            .Verifiable();

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordRefreshTokenRetryAsync(
                    mockContext.Object,
                    mockClient.Object,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("another-refresh-token", response.RefreshToken);
        Assert.Equal("access-token-value", response.AccessToken);
    }

    [Fact]
    public async Task HandleAsync_WhenARotatedTokenIsPresentedAfterTheReuseInterval_AnswersAsReuse()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        SetupLookup(
            mockTokenRequest,
            CreateRotatedGrant(["api", "offline_access"], CreatedWhen.AddSeconds(-60))
        );
        SetupReuseInterval(mockSettings, TimeSpan.FromSeconds(30));
        SetupReuseAnswer(mockContext, mockClient, mockSettings);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenTheLineageEndedWithinTheReuseInterval_RefusesWithoutRevokingTheLineage()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        // The strict mocks have no setup for the replay audit or the lineage revocation: a refused retry is neither.
        SetupLookup(
            mockTokenRequest,
            CreateRotatedGrant(["api", "offline_access"], CreatedWhen.AddSeconds(-10))
        );
        SetupValidation(mockMediator);
        SetupReuseInterval(mockSettings, TimeSpan.FromSeconds(30));

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.ReissueRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>((SecurityToken?)null))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenARetryWithinTheReuseIntervalAsksForNoRefreshToken_RefusesWithoutRevokingTheLineage()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        // The strict mocks have no setup for the reissue, the replay audit or the lineage revocation.
        SetupLookup(mockTokenRequest, CreateRotatedGrant(["api"], CreatedWhen.AddSeconds(-10)));
        SetupValidation(mockMediator);
        SetupReuseInterval(mockSettings, TimeSpan.FromSeconds(30));

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenAConcurrentRequestRotatedTheTokenFirstWithinTheReuseInterval_IssuesAnotherSuccessor()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, _) =
            CreateScaffold();

        // The strict mocks have no setup for the replay audit or the lineage revocation: a retry is neither. The retry
        // audit is the only audit.
        SetupLookup(mockTokenRequest, CreateActiveGrantInLineage(["api", "offline_access"]));
        SetupTokenResponse(mockContext, mockClient, mockMediator);
        SetupReuseInterval(mockSettings, TimeSpan.FromSeconds(30));

        // The first read finds the grant active; the read after the lost rotation finds it rotated by the winner.
        MockPersistedGrantService
            .SetupSequence(x =>
                x.GetOrDefaultAsync<RefreshTokenGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<RefreshTokenGrant>?>(
                    CreateActiveGrantInLineage(["api", "offline_access"])
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<RefreshTokenGrant>?>(
                    CreateRotatedGrant(["api", "offline_access"], CreatedWhen)
                )
            );

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Always)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.RotateRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    CreateGrantId(),
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>((SecurityToken?)null))
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.ReissueRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.Is<CreateSecurityTokenRequest>(request =>
                        request.RefreshTokenLineageId == "lineage-1"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>(CreateSecurityToken("another-refresh-token")))
            .Verifiable();

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordRefreshTokenRetryAsync(
                    mockContext.Object,
                    mockClient.Object,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("another-refresh-token", response.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WhenAConcurrentRequestRotatedTheTokenFirstAndTheGrantWasRevokedOtherwise_AnswersAsReuse()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockSettings, mockError) =
            CreateScaffold();

        // The strict mocks have no setup for the reissue: a grant that no rotation spent is a reuse, not a retry.
        SetupLookup(mockTokenRequest, CreateActiveGrantInLineage(["api", "offline_access"]));
        SetupValidation(mockMediator);
        SetupReuseInterval(mockSettings, TimeSpan.FromSeconds(30));

        // The first read finds the grant active; the read after the lost rotation finds it revoked without a rotation.
        MockPersistedGrantService
            .SetupSequence(x =>
                x.GetOrDefaultAsync<RefreshTokenGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<RefreshTokenGrant>?>(
                    CreateActiveGrantInLineage(["api", "offline_access"])
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<RefreshTokenGrant>?>(
                    CreateRotatedGrant(["api", "offline_access"], CreatedWhen) with
                    {
                        ConsumedWhen = null,
                    }
                )
            );

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.RefreshToken)
            .Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(OpenIdConstants.RefreshTokenRotationPolicy.Always)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.RotateRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    CreateGrantId(),
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken?>((SecurityToken?)null))
            .Verifiable();

        SetupReuseAnswer(mockContext, mockClient, mockSettings);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    #endregion
}
