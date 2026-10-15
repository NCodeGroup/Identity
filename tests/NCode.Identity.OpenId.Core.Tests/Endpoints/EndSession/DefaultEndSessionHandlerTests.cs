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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession;

public class DefaultEndSessionHandlerTests : BaseTests
{
    private const string SubjectId = "subject-1";
    private const string SessionId = "sid-1";
    private const string ClientId = "client-1";
    private const string RedirectUri = "https://rp.example/after-logout";

    private Mock<IOpenIdClientResolver> MockClientResolver { get; }
    private Mock<IFrontChannelLogoutService> MockFrontChannelLogoutService { get; }
    private Mock<ISessionStateService> MockSessionStateService { get; }
    private DefaultEndSessionHandler Handler { get; }

    public DefaultEndSessionHandlerTests()
    {
        MockClientResolver = CreateStrictMock<IOpenIdClientResolver>();
        MockFrontChannelLogoutService = CreateStrictMock<IFrontChannelLogoutService>();
        MockSessionStateService = CreateStrictMock<ISessionStateService>();
        Handler = new DefaultEndSessionHandler(
            MockClientResolver.Object,
            MockFrontChannelLogoutService.Object,
            MockSessionStateService.Object,
            NullLogger<DefaultEndSessionHandler>.Instance
        );
    }

    #region Scaffolding

    private sealed record Scaffold(
        EndSessionCommand Command,
        Mock<OpenIdContext> Context,
        Mock<IMediator> Mediator,
        Mock<IAuthenticationService> AuthenticationService
    );

    private Scaffold CreateScaffold(
        EndSessionSubject subject,
        string? postLogoutRedirectUri,
        string? state
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockRequest = CreateStrictMock<IEndSessionRequest>();
        var mockAuthService = CreateStrictMock<IAuthenticationService>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(mockAuthService.Object);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceCollection.BuildServiceProvider(),
        };

        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.SettingsProvider).Returns(mockSettingsProvider.Object);
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(mockSettings.Object);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.SessionManagementSupported))
            .Returns(false)
            .Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FrontChannelLogoutSupported))
            .Returns(false)
            .Verifiable();

        mockRequest
            .SetupGet(x => x.PostLogoutRedirectUri)
            .Returns(postLogoutRedirectUri)
            .Verifiable();
        mockRequest.SetupGet(x => x.State).Returns(state).Verifiable();

        mockMediator
            .Setup(x =>
                x.SendAsync<ResolveEndSessionSubjectCommand, EndSessionSubject>(
                    It.IsAny<ResolveEndSessionSubjectCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<EndSessionSubject>(subject))
            .Verifiable();
        mockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<OnSessionTerminatedCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockAuthService
            .Setup(x =>
                x.SignOutAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string?>(),
                    It.IsAny<AuthenticationProperties?>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable();

        var command = new EndSessionCommand(mockContext.Object, mockRequest.Object);
        return new Scaffold(command, mockContext, mockMediator, mockAuthService);
    }

    private void SetupResolvedClientWithAllowedUris(IReadOnlyCollection<string> allowedUris)
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();

        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mockClient.Object)
            .Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.PostLogoutRedirectUris, out allowedUris))
            .Returns(true)
            .Verifiable();
    }

    private static EndSessionSubject CreateSubject(string? clientId) =>
        new(SubjectId, SessionId, clientId, [ClientId]);

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoPostLogoutRedirectUri_RunsPipelineAndReturnsNullRedirect()
    {
        var scaffold = CreateScaffold(
            CreateSubject(ClientId),
            postLogoutRedirectUri: null,
            state: null
        );

        var disposition = await Handler.HandleAsync(scaffold.Command, CancellationToken.None);

        Assert.Null(disposition.RedirectUri);
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriProvidedButSubjectClientIdNull_ReturnsNullRedirect()
    {
        var scaffold = CreateScaffold(
            CreateSubject(clientId: null),
            postLogoutRedirectUri: RedirectUri,
            state: null
        );

        var disposition = await Handler.HandleAsync(scaffold.Command, CancellationToken.None);

        Assert.Null(disposition.RedirectUri);
    }

    [Fact]
    public async Task HandleAsync_WhenClientNotResolved_ReturnsNullRedirect()
    {
        var scaffold = CreateScaffold(
            CreateSubject(ClientId),
            postLogoutRedirectUri: RedirectUri,
            state: null
        );
        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((OpenIdClient?)null)
            .Verifiable();

        var disposition = await Handler.HandleAsync(scaffold.Command, CancellationToken.None);

        Assert.Null(disposition.RedirectUri);
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriNotAllowed_ReturnsNullRedirect()
    {
        var scaffold = CreateScaffold(
            CreateSubject(ClientId),
            postLogoutRedirectUri: RedirectUri,
            state: null
        );
        SetupResolvedClientWithAllowedUris(["https://other.example/logout"]);

        var disposition = await Handler.HandleAsync(scaffold.Command, CancellationToken.None);

        Assert.Null(disposition.RedirectUri);
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriAllowed_ReturnsRedirect()
    {
        var scaffold = CreateScaffold(
            CreateSubject(ClientId),
            postLogoutRedirectUri: RedirectUri,
            state: null
        );
        SetupResolvedClientWithAllowedUris([RedirectUri]);

        var disposition = await Handler.HandleAsync(scaffold.Command, CancellationToken.None);

        Assert.Equal(RedirectUri, disposition.RedirectUri);
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriAllowedWithState_AppendsStateQuery()
    {
        var scaffold = CreateScaffold(
            CreateSubject(ClientId),
            postLogoutRedirectUri: RedirectUri,
            state: "abc 123"
        );
        SetupResolvedClientWithAllowedUris([RedirectUri]);

        var disposition = await Handler.HandleAsync(scaffold.Command, CancellationToken.None);

        Assert.Equal($"{RedirectUri}?state=abc%20123", disposition.RedirectUri);
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriHasQueryAndState_AppendsStateWithAmpersand()
    {
        const string uriWithQuery = "https://rp.example/after-logout?foo=bar";
        var scaffold = CreateScaffold(
            CreateSubject(ClientId),
            postLogoutRedirectUri: uriWithQuery,
            state: "xyz"
        );

        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        IReadOnlyCollection<string> allowed = [uriWithQuery];
        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mockClient.Object)
            .Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.PostLogoutRedirectUris, out allowed))
            .Returns(true)
            .Verifiable();

        var disposition = await Handler.HandleAsync(scaffold.Command, CancellationToken.None);

        Assert.Equal($"{uriWithQuery}&state=xyz", disposition.RedirectUri);
    }

    #endregion
}
