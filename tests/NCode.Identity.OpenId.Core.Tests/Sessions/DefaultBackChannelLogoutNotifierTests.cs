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
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
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
    private Mock<IAlgorithmCollectionProvider> MockAlgorithmCollectionProvider { get; }
    private Mock<ICredentialSelector> MockCredentialSelector { get; }
    private Mock<IJsonWebTokenService> MockJsonWebTokenService { get; }
    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private RecordingHttpMessageHandler Handler { get; }
    private DefaultBackChannelLogoutNotifier Notifier { get; }

    public DefaultBackChannelLogoutNotifierTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockAlgorithmCollectionProvider = CreateStrictMock<IAlgorithmCollectionProvider>();
        MockCredentialSelector = CreateStrictMock<ICredentialSelector>();
        MockJsonWebTokenService = CreateStrictMock<IJsonWebTokenService>();
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        Handler = new RecordingHttpMessageHandler();

        Notifier = new DefaultBackChannelLogoutNotifier(
            MockTimeProvider.Object,
            MockCryptoService.Object,
            MockAlgorithmCollectionProvider.Object,
            MockCredentialSelector.Object,
            MockJsonWebTokenService.Object,
            MockHttpClientFactory.Object,
            NullLogger<DefaultBackChannelLogoutNotifier>.Instance
        );
    }

    #region Scaffolding

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public bool Throw { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (Throw)
            {
                throw new HttpRequestException("unreachable");
            }

            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private Mock<OpenIdContext> CreateContext()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSecretsProvider = CreateLooseMock<ISecretKeyCollectionProvider>();
        var mockSecretKeys = CreateLooseMock<ISecretKeyCollection>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer);
        mockTenant
            .SetupGet(x => x.SecretsProvider)
            .Returns(mockSecretsProvider.Object)
            .Verifiable();
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(mockSecretKeys.Object);

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

    private void SetupSigningCredentials(Mock<OpenIdClient> mockClient, bool found)
    {
        IReadOnlyCollection<string> algValues = ["RS256"];
        Mock.Get(mockClient.Object.Settings)
            .Setup(x => x.GetValue(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported))
            .Returns(algValues)
            .Verifiable();

        var mockAlgorithms = CreateLooseMock<IAlgorithmCollection>();
        MockAlgorithmCollectionProvider
            .SetupGet(x => x.Collection)
            .Returns(mockAlgorithms.Object)
            .Verifiable();

        JoseSigningCredentials credentials = found
            ? new JoseSigningCredentials(
                CreateLooseMock<SecretKey>().Object,
                CreateLooseMock<SignatureAlgorithm>().Object
            )
            : null!;
        MockCredentialSelector
            .Setup(x =>
                x.TryGetSigningCredentials(
                    It.IsAny<IAlgorithmCollection>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IReadOnlyCollection<SecretKey>>(),
                    out credentials
                )
            )
            .Returns(found)
            .Verifiable();
    }

    private void SetupTokenGeneration(EncodeJwtParameters[] captured)
    {
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), BinaryEncodingType.Base64Url))
            .Returns("jti-value")
            .Verifiable();
        MockJsonWebTokenService
            .Setup(x => x.EncodeJwt(It.IsAny<EncodeJwtParameters>()))
            .Callback((EncodeJwtParameters parameters) => captured[0] = parameters)
            .Returns(LogoutToken)
            .Verifiable();
    }

    private void SetupHttpClient() =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(Handler))
            .Verifiable();

    #endregion

    #region NotifyAsync Tests

    [Fact]
    public async Task NotifyAsync_WhenConfigured_PostsSignedLogoutTokenToBackChannelUri()
    {
        var mockContext = CreateContext();
        var mockClient = CreateClient(LogoutUri);
        SetupClientId(mockClient);
        SetupSigningCredentials(mockClient, found: true);
        var captured = new EncodeJwtParameters[1];
        SetupTokenGeneration(captured);
        SetupHttpClient();

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            SubjectId,
            SessionId,
            CancellationToken.None
        );

        Assert.Equal(new Uri(LogoutUri), Handler.RequestUri);
        Assert.NotNull(Handler.RequestBody);
        Assert.Contains(OpenIdConstants.Parameters.LogoutToken, Handler.RequestBody);

        var parameters = captured[0];
        Assert.Equal(OpenIdConstants.SecurityTokenTypes.LogoutToken, parameters.TokenType);
        Assert.Equal(Issuer, parameters.Issuer);
        Assert.Equal(ClientId, parameters.Audience);
        Assert.NotNull(parameters.ExtraPayloadClaims);
        Assert.Equal(SubjectId, parameters.ExtraPayloadClaims[JoseClaimNames.Payload.Sub]);
        Assert.Equal(SessionId, parameters.ExtraPayloadClaims[JoseClaimNames.Payload.Sid]);
        Assert.True(
            parameters.ExtraPayloadClaims.ContainsKey(
                OpenIdConstants.BackChannelLogout.EventsClaimName
            )
        );
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

        Assert.Null(Handler.RequestUri);
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

        Assert.Null(Handler.RequestUri);
    }

    [Fact]
    public async Task NotifyAsync_WhenNoSigningCredentials_DoesNothing()
    {
        var mockContext = CreateContext();
        var mockClient = CreateClient(LogoutUri);
        SetupSigningCredentials(mockClient, found: false);

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            SubjectId,
            SessionId,
            CancellationToken.None
        );

        Assert.Null(Handler.RequestUri);
    }

    [Fact]
    public async Task NotifyAsync_WhenDeliveryFails_SwallowsException()
    {
        var mockContext = CreateContext();
        var mockClient = CreateClient(LogoutUri);
        SetupClientId(mockClient);
        SetupSigningCredentials(mockClient, found: true);
        var captured = new EncodeJwtParameters[1];
        SetupTokenGeneration(captured);
        SetupHttpClient();
        Handler.Throw = true;

        await Notifier.NotifyAsync(
            mockContext.Object,
            mockClient.Object,
            SubjectId,
            SessionId,
            CancellationToken.None
        );
    }

    #endregion
}
