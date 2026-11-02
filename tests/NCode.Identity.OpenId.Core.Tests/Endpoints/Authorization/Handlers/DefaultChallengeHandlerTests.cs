// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Handlers;

public class DefaultChallengeHandlerTests : BaseTests
{
    private DefaultChallengeHandler Handler { get; } = new();

    [Fact]
    public async Task HandleAsync_Always_ChallengesAndReturnsHandled()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockAuthRequest = CreateStrictMock<IAuthorizationRequest>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockAuthService = CreateStrictMock<IAuthenticationService>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(mockAuthService.Object);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceCollection.BuildServiceProvider(),
        };

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockAuthService
            .Setup(x =>
                x.ChallengeAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string?>(),
                    It.IsAny<AuthenticationProperties?>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable();

        var command = new ChallengeCommand(
            mockContext.Object,
            mockClient.Object,
            mockAuthRequest.Object,
            new AuthenticationProperties()
        );

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.WasHandled);
    }
}
