// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultBackChannelLogoutOnSessionTerminatedHandlerTests : BaseTests
{
    private Mock<IOpenIdClientResolver> MockClientResolver { get; }
    private Mock<IBackChannelLogoutNotifier> MockNotifier { get; }
    private DefaultBackChannelLogoutOnSessionTerminatedHandler Handler { get; }

    public DefaultBackChannelLogoutOnSessionTerminatedHandlerTests()
    {
        MockClientResolver = CreateStrictMock<IOpenIdClientResolver>();
        MockNotifier = CreateStrictMock<IBackChannelLogoutNotifier>();
        Handler = new DefaultBackChannelLogoutOnSessionTerminatedHandler(
            MockClientResolver.Object,
            MockNotifier.Object
        );
    }

    [Fact]
    public async Task HandleAsync_NotifiesEachResolvedParticipatingClient()
    {
        var context = Mock.Of<OpenIdContext>();
        var command = new OnSessionTerminatedCommand(
            context,
            new SessionTermination(
                SessionTerminationReasons.Logout,
                "subject-1",
                "sid-1",
                ["c1", "c2"]
            )
        );

        var client1 = CreateStrictMock<OpenIdClient>().Object;

        MockClientResolver
            .Setup(x => x.ResolveClientOrDefaultAsync(context, "c1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client1)
            .Verifiable();
        // c2 does not resolve (disabled/removed) and is therefore skipped.
        MockClientResolver
            .Setup(x => x.ResolveClientOrDefaultAsync(context, "c2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpenIdClient?)null)
            .Verifiable();

        MockNotifier
            .Setup(x =>
                x.NotifyAsync(context, client1, "subject-1", "sid-1", It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }
}
