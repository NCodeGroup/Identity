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
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Dispatch;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultBackChannelLogoutNotifierTests : BaseTests
{
    private const string Issuer = "https://op.example";
    private const string ClientId = "client-1";
    private const string SubjectId = "subject-1";
    private const string SessionId = "sid-1";
    private const string LogoutUri = "https://rp.example/backchannel-logout";
    private const string LogoutToken = "header.payload.signature";

    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IClientJwtCredentialSelector> MockClientJwtCredentialSelector { get; }
    private Mock<IJwtEncoder> MockJsonWebTokenService { get; }
    private Mock<ISubjectIdentifierService> MockSubjectIdentifierService { get; }
    private Mock<IJobDispatcher> MockJobDispatcher { get; }
    private Mock<ILogger<DefaultBackChannelLogoutNotifier>> MockLogger { get; }
    private DefaultBackChannelLogoutNotifier Notifier { get; }

    private BackChannelLogoutDeliveryJob? CapturedJob { get; set; }

    public DefaultBackChannelLogoutNotifierTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockClientJwtCredentialSelector = CreateStrictMock<IClientJwtCredentialSelector>();
        MockJsonWebTokenService = CreateStrictMock<IJwtEncoder>();
        MockSubjectIdentifierService = CreateStrictMock<ISubjectIdentifierService>();
        MockJobDispatcher = CreateStrictMock<IJobDispatcher>();
        MockLogger = CreateStrictMock<ILogger<DefaultBackChannelLogoutNotifier>>();

        Notifier = new DefaultBackChannelLogoutNotifier(
            MockTimeProvider.Object,
            MockCryptoService.Object,
            MockClientJwtCredentialSelector.Object,
            MockJsonWebTokenService.Object,
            MockSubjectIdentifierService.Object,
            MockJobDispatcher.Object,
            MockLogger.Object
        );
    }

    #region Scaffolding

    private Mock<OpenIdContext> CreateContext()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer);

        return mockContext;
    }

    private Mock<OpenIdClient> CreateClient(string? backChannelLogoutUri)
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();

        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.BackChannelLogoutUri, out backChannelLogoutUri)
            )
            .Returns(!string.IsNullOrEmpty(backChannelLogoutUri))
            .Verifiable();

        return mockClient;
    }

    private void SetupClientId(Mock<OpenIdClient> mockClient) =>
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();

    private JoseSigningCredentials CreateSigningCredentials() =>
        new(CreateLooseMock<SecretKey>().Object, CreateLooseMock<SignatureAlgorithm>().Object);

    private void SetupCredentials(
        Mock<OpenIdContext> mockContext,
        Mock<OpenIdClient> mockClient,
        ClientJwtCredentials credentials
    ) =>
        MockClientJwtCredentialSelector
            .Setup(x =>
                x.SelectCredentialsAsync(
                    mockContext.Object,
                    mockClient.Object,
                    ClientJwtProtectionSettings.IdToken,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(credentials)
            .Verifiable();

    private void SetupTokenGeneration(JwtEncodingParameters[] captured)
    {
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), BinaryEncodings.Base64Url))
            .Returns("jti-value")
            .Verifiable();
        MockJsonWebTokenService
            .Setup(x => x.Encode(It.IsAny<JwtEncodingParameters>()))
            .Callback((JwtEncodingParameters parameters) => captured[0] = parameters)
            .Returns(LogoutToken)
            .Verifiable();
    }

    private void SetupDispatch() =>
        MockJobDispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.IsAny<BackChannelLogoutDeliveryJob>(),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (BackChannelLogoutDeliveryJob job, DispatchOptions? _, CancellationToken _) =>
                    CapturedJob = job
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupNotProtectedWarning(Exception? exception)
    {
        MockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        MockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId =>
                        eventId.Id == EventIds.BackChannelLogoutTokenNotProtected
                    ),
                    It.IsAny<It.IsAnyType>(),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
    }

    #endregion

    #region NotifyAsync Tests

    [Fact]
    public async Task NotifyAsync_WhenConfigured_DispatchesSignedLogoutTokenDeliveryJob()
    {
        var mockContext = CreateContext();
        var mockClient = CreateClient(LogoutUri);
        SetupClientId(mockClient);
        SetupCredentials(
            mockContext,
            mockClient,
            new ClientJwtCredentials(CreateSigningCredentials(), null)
        );
        MockSubjectIdentifierService
            .Setup(x => x.ProjectSubjectId(mockContext.Object, mockClient.Object, SubjectId))
            .Returns("pairwise-sub")
            .Verifiable();
        var captured = new JwtEncodingParameters[1];
        SetupTokenGeneration(captured);
        SetupDispatch();

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            SubjectId,
            SessionId,
            CancellationToken.None
        );

        MockJobDispatcher.Verify();
        Assert.Equal(LogoutUri, CapturedJob?.LogoutUri);
        Assert.Equal(ClientId, CapturedJob?.ClientId);
        Assert.Equal(LogoutToken, CapturedJob?.LogoutToken);

        var parameters = captured[0];
        Assert.Equal(JoseTokenTypes.LogoutJwt, parameters.TokenType);
        Assert.Null(parameters.EncryptionCredentials);
        Assert.Equal(Issuer, parameters.Issuer);
        Assert.Equal(ClientId, parameters.Audience);
        Assert.NotNull(parameters.ExtraPayloadClaims);
        // the logout_token carries the projected (pairwise) subject, not the raw principal id
        Assert.Equal("pairwise-sub", parameters.ExtraPayloadClaims[JoseClaimNames.Payload.Sub]);
        Assert.Equal(SessionId, parameters.ExtraPayloadClaims[JoseClaimNames.Payload.Sid]);
        Assert.True(parameters.ExtraPayloadClaims.ContainsKey(JoseClaimNames.Payload.Events));
    }

    [Fact]
    public async Task NotifyAsync_WhenNoBackChannelUri_DoesNothing()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateClient(backChannelLogoutUri: null);

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            SubjectId,
            SessionId,
            CancellationToken.None
        );

        Assert.Null(CapturedJob);
    }

    [Fact]
    public async Task NotifyAsync_WhenNoSubjectAndNoSession_DoesNothing()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateClient(LogoutUri);

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            subjectId: null,
            sessionId: null,
            CancellationToken.None
        );

        Assert.Null(CapturedJob);
    }

    [Fact]
    public async Task NotifyAsync_WhenClientRegistersIdTokenEncryption_EncryptsLogoutTokenWithIssuerInHeader()
    {
        var mockContext = CreateContext();
        var mockClient = CreateClient(LogoutUri);
        SetupClientId(mockClient);
        var encryptionCredentials = new JoseEncryptionCredentials(
            CreateLooseMock<SecretKey>().Object,
            CreateLooseMock<KeyManagementAlgorithm>().Object,
            CreateLooseMock<AuthenticatedEncryptionAlgorithm>().Object
        );
        SetupCredentials(
            mockContext,
            mockClient,
            new ClientJwtCredentials(CreateSigningCredentials(), encryptionCredentials)
        );
        var captured = new JwtEncodingParameters[1];
        SetupTokenGeneration(captured);
        SetupDispatch();

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            subjectId: null,
            SessionId,
            CancellationToken.None
        );

        var parameters = captured[0];
        Assert.Same(encryptionCredentials, parameters.EncryptionCredentials);
        Assert.NotNull(parameters.ExtraEncryptionHeaderClaims);
        Assert.Equal(Issuer, parameters.ExtraEncryptionHeaderClaims[JoseClaimNames.Payload.Iss]);
    }

    [Fact]
    public async Task NotifyAsync_WhenRegistrationCannotBeHonored_LogsWarningAndDoesNothing()
    {
        var mockContext = CreateContext();
        var mockClient = CreateClient(LogoutUri);
        SetupClientId(mockClient);
        var exception = new OpenIdException(CreateLooseMock<IOpenIdError>().Object);
        MockClientJwtCredentialSelector
            .Setup(x =>
                x.SelectCredentialsAsync(
                    mockContext.Object,
                    mockClient.Object,
                    ClientJwtProtectionSettings.IdToken,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception)
            .Verifiable();
        SetupNotProtectedWarning(exception);

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            SubjectId,
            SessionId,
            CancellationToken.None
        );

        Assert.Null(CapturedJob);
    }

    [Fact]
    public async Task NotifyAsync_WhenNoSigningCredentials_LogsWarningAndDoesNothing()
    {
        var mockContext = CreateContext();
        var mockClient = CreateClient(LogoutUri);
        SetupClientId(mockClient);
        SetupCredentials(mockContext, mockClient, new ClientJwtCredentials(null, null));
        SetupNotProtectedWarning(exception: null);

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            SubjectId,
            SessionId,
            CancellationToken.None
        );

        Assert.Null(CapturedJob);
    }
    #endregion
}
