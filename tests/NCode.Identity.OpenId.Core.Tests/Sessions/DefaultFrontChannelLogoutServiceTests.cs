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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultFrontChannelLogoutServiceTests : BaseTests
{
    private const string Issuer = "https://op.example";
    private const string ClientId = "client-1";
    private const string SubjectId = "subject-1";
    private const string SessionId = "sid-1";
    private const string LogoutUri = "https://rp.example/frontchannel-logout";

    private Mock<IOpenIdClientResolver> MockClientResolver { get; }
    private DefaultFrontChannelLogoutService Service { get; }

    public DefaultFrontChannelLogoutServiceTests()
    {
        MockClientResolver = CreateStrictMock<IOpenIdClientResolver>();
        Service = new DefaultFrontChannelLogoutService(MockClientResolver.Object);
    }

    #region Scaffolding

    private Mock<OpenIdContext> CreateContext(bool sessionSupported = true)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer);
        mockTenant.SetupGet(x => x.SettingsProvider).Returns(mockSettingsProvider.Object);
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(mockTenantSettings.Object);
        mockTenantSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FrontChannelLogoutSessionSupported))
            .Returns(sessionSupported);
        return mockContext;
    }

    private Mock<OpenIdClient> SetupResolvedClient(string clientId)
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    clientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mockClient.Object)
            .Verifiable();
        return mockClient;
    }

    private static EndSessionSubject CreateSubject(params string[] clientIds) =>
        new(SubjectId, SessionId, ClientId, clientIds);

    #endregion

    [Fact]
    public async Task GetLogoutUrisAsync_WhenNoParticipatingClients_ReturnsEmpty()
    {
        var mockContext = CreateContext();

        var result = await Service.GetLogoutUrisAsync(
            mockContext.Object,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLogoutUrisAsync_WhenSessionNotRequired_ReturnsUriAsIs()
    {
        var mockContext = CreateContext();
        var mockClient = SetupResolvedClient(ClientId);
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);

        string? uri = LogoutUri;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.FrontChannelLogoutUri, out uri))
            .Returns(true);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FrontChannelLogoutSessionRequired))
            .Returns(false);

        var result = await Service.GetLogoutUrisAsync(
            mockContext.Object,
            CreateSubject(ClientId),
            CancellationToken.None
        );

        Assert.Equal([LogoutUri], result);
    }

    [Fact]
    public async Task GetLogoutUrisAsync_WhenSessionRequired_AppendsIssAndSid()
    {
        var mockContext = CreateContext();
        var mockClient = SetupResolvedClient(ClientId);
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);

        string? uri = LogoutUri;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.FrontChannelLogoutUri, out uri))
            .Returns(true);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FrontChannelLogoutSessionRequired))
            .Returns(true);

        var result = await Service.GetLogoutUrisAsync(
            mockContext.Object,
            CreateSubject(ClientId),
            CancellationToken.None
        );

        var single = Assert.Single(result);
        Assert.StartsWith($"{LogoutUri}?iss=", single);
        Assert.Contains($"iss={Uri.EscapeDataString(Issuer)}", single);
        Assert.Contains($"sid={Uri.EscapeDataString(SessionId)}", single);
    }

    [Fact]
    public async Task GetLogoutUrisAsync_WhenClientHasNoFrontChannelUri_SkipsClient()
    {
        var mockContext = CreateContext();
        var mockClient = SetupResolvedClient(ClientId);
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);

        string? uri = null;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.FrontChannelLogoutUri, out uri))
            .Returns(false);

        var result = await Service.GetLogoutUrisAsync(
            mockContext.Object,
            CreateSubject(ClientId),
            CancellationToken.None
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLogoutUrisAsync_WhenSessionRequiredButUnsupported_ReturnsUriAsIs()
    {
        var mockContext = CreateContext(sessionSupported: false);
        var mockClient = SetupResolvedClient(ClientId);
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);

        string? uri = LogoutUri;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.FrontChannelLogoutUri, out uri))
            .Returns(true);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FrontChannelLogoutSessionRequired))
            .Returns(true);

        var result = await Service.GetLogoutUrisAsync(
            mockContext.Object,
            CreateSubject(ClientId),
            CancellationToken.None
        );

        Assert.Equal([LogoutUri], result);
    }
}
