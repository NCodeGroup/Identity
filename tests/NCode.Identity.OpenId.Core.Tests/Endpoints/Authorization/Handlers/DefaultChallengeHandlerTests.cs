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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
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

        var services = new ServiceCollection();
        services.AddSingleton(mockAuthService.Object);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
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
