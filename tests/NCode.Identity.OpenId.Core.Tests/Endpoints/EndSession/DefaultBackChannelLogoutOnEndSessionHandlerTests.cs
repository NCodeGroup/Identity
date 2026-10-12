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
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession;

public class DefaultBackChannelLogoutOnEndSessionHandlerTests : BaseTests
{
    private const string SubjectId = "subject-1";
    private const string SessionId = "sid-1";

    private Mock<IOpenIdClientResolver> MockClientResolver { get; }
    private Mock<IBackChannelLogoutNotifier> MockNotifier { get; }
    private DefaultBackChannelLogoutOnEndSessionHandler Handler { get; }

    public DefaultBackChannelLogoutOnEndSessionHandlerTests()
    {
        MockClientResolver = CreateStrictMock<IOpenIdClientResolver>();
        MockNotifier = CreateStrictMock<IBackChannelLogoutNotifier>();
        Handler = new DefaultBackChannelLogoutOnEndSessionHandler(
            MockClientResolver.Object,
            MockNotifier.Object
        );
    }

    #region Scaffolding

    private OnEndSessionCommand CreateCommand(
        OpenIdContext context,
        IReadOnlyList<string> participatingClientIds
    ) => new(context, new EndSessionSubject(SubjectId, SessionId, null, participatingClientIds));

    private void SetupResolveClient(OpenIdContext context, string clientId, OpenIdClient? client) =>
        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(context, clientId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(client)
            .Verifiable();

    private void SetupNotify(OpenIdContext context, OpenIdClient client) =>
        MockNotifier
            .Setup(x =>
                x.NotifyAsync(context, client, SubjectId, SessionId, It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_NotifiesEachResolvedClient()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient1 = CreateStrictMock<OpenIdClient>();
        var mockClient2 = CreateStrictMock<OpenIdClient>();

        SetupResolveClient(mockContext.Object, "client-1", mockClient1.Object);
        SetupResolveClient(mockContext.Object, "client-2", mockClient2.Object);
        SetupNotify(mockContext.Object, mockClient1.Object);
        SetupNotify(mockContext.Object, mockClient2.Object);

        await Handler.HandleAsync(
            CreateCommand(mockContext.Object, ["client-1", "client-2"]),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_SkipsClientsThatCannotBeResolved()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();

        SetupResolveClient(mockContext.Object, "client-1", client: null);
        SetupResolveClient(mockContext.Object, "client-2", mockClient.Object);
        SetupNotify(mockContext.Object, mockClient.Object);

        await Handler.HandleAsync(
            CreateCommand(mockContext.Object, ["client-1", "client-2"]),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoParticipatingClients_DoesNothing()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();

        await Handler.HandleAsync(CreateCommand(mockContext.Object, []), CancellationToken.None);
    }

    #endregion
}
