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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens;

public sealed class DefaultTokenServiceTests : BaseTests, IAsyncLifetime
{
    private const string Issuer = "https://op.test/tenant";
    private const string Audience = "urn:rs-api";

    private static DateTimeOffset CreatedWhen { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _serviceProvider;
    private readonly RSA _tenantRsa = RSA.Create(2048);

    public DefaultTokenServiceTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        var tenantKey = _serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "tenant" }, _tenantRsa);
        Assert.True(
            _serviceProvider
                .GetRequiredService<ICredentialSelector>()
                .TryGetSigningCredentials(
                    _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection,
                    [AlgorithmCodes.DigitalSignature.RsaSsaPssSha256],
                    [tenantKey],
                    out var signingCredentials
                )
        );
        SigningCredentials = signingCredentials;

        MockPolicyService = CreateStrictMock<IAccessTokenPolicyService>();
        MockJwtEncoder = CreateStrictMock<IJwtEncoder>();
        MockResolver = CreateStrictMock<IResourceServerCredentialResolver>();
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        MockMediator = CreateStrictMock<IMediator>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockClient = CreateStrictMock<OpenIdClient>();
        MockResourceServerSecrets = CreateStrictMock<ISecretKeyCollectionProvider>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();

        Service = new DefaultTokenService(
            MockCryptoService.Object,
            CreateStrictMock<IClientJwtCredentialSelector>().Object,
            MockPolicyService.Object,
            MockJwtEncoder.Object,
            MockPersistedGrantService.Object,
            MockResolver.Object,
            MockAuditEventRecorder.Object
        );
    }

    private JoseSigningCredentials SigningCredentials { get; }
    private Mock<IAccessTokenPolicyService> MockPolicyService { get; }
    private Mock<IJwtEncoder> MockJwtEncoder { get; }
    private Mock<IResourceServerCredentialResolver> MockResolver { get; }
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private Mock<IMediator> MockMediator { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdClient> MockClient { get; }
    private Mock<ISecretKeyCollectionProvider> MockResourceServerSecrets { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private DefaultTokenService Service { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        _tenantRsa.Dispose();
    }

    #region Scaffolding

    private static CreateSecurityTokenRequest CreateTokenRequest() =>
        new()
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdConstants.GrantTypes.ClientCredentials,
            OriginalScopes = ["read:x"],
            EffectiveScopes = ["read:x"],
            Confirmation = new TokenConfirmation { JwkThumbprint = "jkt-thumbprint" },
        };

    // The resolution owns the resource server's secret-key provider, so its disposal is verified.
    private ResourceServerCredentialResolution SetupResolution(
        CreateSecurityTokenRequest tokenRequest
    )
    {
        MockResourceServerSecrets
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var resolution = new ResourceServerCredentialResolution
        {
            ResourceServers =
            [
                new ResolvedResourceServer
                {
                    ResourceServerId = "rs-api",
                    Identifier = Audience,
                    Scopes = [],
                    IsSystem = false,
                    EffectiveSettings = CreateLooseMock<IReadOnlySettingCollection>().Object,
                    SecretsProvider = MockResourceServerSecrets.Object,
                },
            ],
        };

        MockResolver
            .Setup(x =>
                x.ResolveAsync(
                    MockContext.Object,
                    tokenRequest.EffectiveScopes,
                    It.Is<IReadOnlyCollection<string>>(resources => resources.Count == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(resolution)
            .Verifiable();

        return resolution;
    }

    private void SetupContext()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer).Verifiable();
        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        MockContext.SetupGet(x => x.Mediator).Returns(MockMediator.Object).Verifiable();
    }

    private void SetupClaimsCommands()
    {
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<GetAccessTokenSubjectClaimsCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<GetAccessTokenPayloadClaimsCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    // The policy's settings are a value-provider stub: the token service reads them, it does not combine them.
    private IReadOnlySettingCollection CreatePolicySettings(TimeSpan lifetime)
    {
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.AccessTokenLifetime))
            .Returns(lifetime);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.AccessTokenType))
            .Returns(JoseTokenTypes.AccessTokenJwt);
        return mockSettings.Object;
    }

    // Everything the refresh-token path reads from the request context and the client.
    private void SetupRefreshTokenContext()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.TenantId).Returns("tenant-1").Verifiable();
        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();

        MockClient.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();

        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenLifetime))
            .Returns(TimeSpan.FromDays(30))
            .Verifiable();
        MockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
    }

    private static CreateSecurityTokenRequest CreateRotationRequest() =>
        new()
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdConstants.GrantTypes.RefreshToken,
            OriginalScopes = ["offline_access"],
            EffectiveScopes = ["offline_access"],
            RefreshTokenLineageId = "lineage-1",
        };

    private static PersistedGrantId CreateRefreshTokenGrantId(string grantKey) =>
        new()
        {
            TenantId = "tenant-1",
            GrantType = OpenIdConstants.PersistedGrantTypes.RefreshToken,
            GrantKey = grantKey,
        };

    // Everything the token service does before it spends the presented token and stores the successor.
    private PersistedGrantId SetupSuccessor()
    {
        SetupRefreshTokenContext();

        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodings.Base64Url))
            .Returns("successor")
            .Verifiable();

        var successorId = CreateRefreshTokenGrantId("successor");
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    "tenant-1",
                    OpenIdConstants.PersistedGrantTypes.RefreshToken,
                    "successor"
                )
            )
            .Returns(successorId)
            .Verifiable();

        return successorId;
    }

    private void SetupReplace(
        PersistedGrantId presentedId,
        PersistedGrantId successorId,
        string? replacementGrantKey
    ) =>
        MockPersistedGrantService
            .Setup(x =>
                x.ReplaceOnceOrDefault(
                    MockContext.Object,
                    presentedId,
                    successorId,
                    It.Is<PersistedGrant<RefreshTokenGrant>>(grant =>
                        grant.RefreshTokenLineageId == "lineage-1" && grant.ClientId == "client-1"
                    ),
                    CreatedWhen,
                    TimeSpan.FromDays(30),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<string?>(replacementGrantKey))
            .Verifiable();

    private void SetupAddToLineage(PersistedGrantId successorId, string? grantKey) =>
        MockPersistedGrantService
            .Setup(x =>
                x.AddToRefreshTokenLineageOrDefault(
                    MockContext.Object,
                    successorId,
                    It.Is<PersistedGrant<RefreshTokenGrant>>(grant =>
                        grant.RefreshTokenLineageId == "lineage-1" && grant.ClientId == "client-1"
                    ),
                    CreatedWhen,
                    TimeSpan.FromDays(30),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<string?>(grantKey))
            .Verifiable();

    #endregion

    #region CreateAccessTokenAsync Tests

    [Fact]
    public async Task CreateAccessTokenAsync_WhenThePolicyAllowsTheToken_IssuesItUnderThePolicy()
    {
        var tokenRequest = CreateTokenRequest();
        var resolution = SetupResolution(tokenRequest);
        SetupContext();
        SetupClaimsCommands();

        var policy = new AccessTokenPolicy
        {
            Settings = CreatePolicySettings(TimeSpan.FromMinutes(2)),
            SigningCredentials = SigningCredentials,
        };
        MockPolicyService
            .Setup(x =>
                x.Evaluate(
                    MockContext.Object,
                    MockClient.Object,
                    tokenRequest.Confirmation,
                    resolution
                )
            )
            .Returns(policy)
            .Verifiable();

        JwtEncodingParameters? encoded = null;
        MockJwtEncoder
            .Setup(x => x.Encode(It.IsAny<JwtEncodingParameters>()))
            .Callback((JwtEncodingParameters parameters) => encoded = parameters)
            .Returns("access-token-jwt")
            .Verifiable();
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordTokenIssuedAsync(
                    MockContext.Object,
                    MockClient.Object,
                    null,
                    It.IsAny<SecurityToken>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var securityToken = await Service.CreateAccessTokenAsync(
            MockContext.Object,
            MockClient.Object,
            tokenRequest,
            CancellationToken.None
        );

        Assert.Equal("access-token-jwt", securityToken.TokenValue);
        Assert.Equal(CreatedWhen + TimeSpan.FromMinutes(2), securityToken.TokenLifetime.EndTime);
        Assert.NotNull(encoded);
        Assert.Same(SigningCredentials, encoded.SigningCredentials);
        Assert.Null(encoded.EncryptionCredentials);
        Assert.Equal(JoseTokenTypes.AccessTokenJwt, encoded.TokenType);
        Assert.Equal(Issuer, encoded.Issuer);
        Assert.Equal(Audience, encoded.Audience.ToString());
        Assert.Equal(CreatedWhen + TimeSpan.FromMinutes(2), encoded.Expires);
    }

    [Fact]
    public async Task CreateAccessTokenAsync_WhenThePolicyRefusesTheToken_PropagatesAndReleasesTheResolution()
    {
        var tokenRequest = CreateTokenRequest();
        var resolution = SetupResolution(tokenRequest);
        MockContext.SetupGet(x => x.Mediator).Returns(MockMediator.Object).Verifiable();
        MockContext
            .SetupGet(x => x.Tenant)
            .Returns(CreateStrictMock<OpenIdTenant>().Object)
            .Verifiable();

        var refusal = new OpenIdException(CreateLooseMock<IOpenIdError>().Object);
        MockPolicyService
            .Setup(x =>
                x.Evaluate(
                    MockContext.Object,
                    MockClient.Object,
                    tokenRequest.Confirmation,
                    resolution
                )
            )
            .Throws(refusal)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Service.CreateAccessTokenAsync(
                MockContext.Object,
                MockClient.Object,
                tokenRequest,
                CancellationToken.None
            )
        );

        Assert.Same(refusal, exception);
    }

    #endregion

    #region RotateRefreshTokenAsync Tests

    [Fact]
    public async Task RotateRefreshTokenAsync_WhenThePresentedTokenIsSpent_ReturnsTheSuccessorInItsLineage()
    {
        var presentedId = CreateRefreshTokenGrantId("presented");
        var successorId = SetupSuccessor();
        SetupReplace(presentedId, successorId, "grant-2");
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordTokenIssuedAsync(
                    MockContext.Object,
                    MockClient.Object,
                    null,
                    It.IsAny<SecurityToken>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var securityToken = await Service.RotateRefreshTokenAsync(
            MockContext.Object,
            MockClient.Object,
            presentedId,
            CreateRotationRequest(),
            CancellationToken.None
        );

        Assert.NotNull(securityToken);
        Assert.Equal("successor", securityToken.Value.TokenValue);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_WhenAConcurrentRequestSpentThePresentedToken_ReturnsNullWithoutAuditing()
    {
        var presentedId = CreateRefreshTokenGrantId("presented");
        var successorId = SetupSuccessor();
        SetupReplace(presentedId, successorId, null);

        var securityToken = await Service.RotateRefreshTokenAsync(
            MockContext.Object,
            MockClient.Object,
            presentedId,
            CreateRotationRequest(),
            CancellationToken.None
        );

        Assert.Null(securityToken);
    }

    #endregion

    #region ReissueRefreshTokenAsync Tests

    [Fact]
    public async Task ReissueRefreshTokenAsync_WhenTheLineageIsActive_ReturnsTheTokenInItsLineage()
    {
        var successorId = SetupSuccessor();
        SetupAddToLineage(successorId, "grant-2");
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordTokenIssuedAsync(
                    MockContext.Object,
                    MockClient.Object,
                    null,
                    It.IsAny<SecurityToken>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var securityToken = await Service.ReissueRefreshTokenAsync(
            MockContext.Object,
            MockClient.Object,
            CreateRotationRequest(),
            CancellationToken.None
        );

        Assert.NotNull(securityToken);
        Assert.Equal("successor", securityToken.Value.TokenValue);
    }

    [Fact]
    public async Task ReissueRefreshTokenAsync_WhenNoGrantOfTheLineageIsActive_ReturnsNullWithoutAuditing()
    {
        var successorId = SetupSuccessor();
        SetupAddToLineage(successorId, null);

        var securityToken = await Service.ReissueRefreshTokenAsync(
            MockContext.Object,
            MockClient.Object,
            CreateRotationRequest(),
            CancellationToken.None
        );

        Assert.Null(securityToken);
    }

    [Fact]
    public async Task ReissueRefreshTokenAsync_WhenTheRequestNamesNoLineage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service.ReissueRefreshTokenAsync(
                MockContext.Object,
                MockClient.Object,
                CreateRotationRequest() with
                {
                    RefreshTokenLineageId = null,
                },
                CancellationToken.None
            )
        );
    }

    #endregion
}
