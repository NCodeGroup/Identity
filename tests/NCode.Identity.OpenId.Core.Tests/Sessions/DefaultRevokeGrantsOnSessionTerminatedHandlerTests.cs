// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultRevokeGrantsOnSessionTerminatedHandlerTests : BaseTests
{
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private DefaultRevokeGrantsOnSessionTerminatedHandler Handler { get; }

    public DefaultRevokeGrantsOnSessionTerminatedHandlerTests()
    {
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        Handler = new DefaultRevokeGrantsOnSessionTerminatedHandler(
            TimeProvider.System,
            MockPersistedGrantService.Object
        );
    }

    private static OnSessionTerminatedCommand CreateCommand(
        string reason,
        string? subjectId,
        string? sessionId
    ) => new(Mock.Of<OpenIdContext>(), new SessionTermination(reason, subjectId, sessionId, []));

    [Fact]
    public async Task HandleAsync_WhenSessionScoped_RevokesBySession()
    {
        MockPersistedGrantService
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.IsAny<OpenIdContext>(),
                    It.Is<GrantFilter>(f => f.SessionId == "sid-1" && f.SubjectId == null),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<long>(2L))
            .Verifiable();

        await Handler.HandleAsync(
            CreateCommand(SessionTerminationReasons.AdministrativeRevocation, "subject-1", "sid-1"),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectWide_RevokesBySubject()
    {
        MockPersistedGrantService
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.IsAny<OpenIdContext>(),
                    It.Is<GrantFilter>(f => f.SubjectId == "subject-1" && f.SessionId == null),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<long>(5L))
            .Verifiable();

        await Handler.HandleAsync(
            CreateCommand(SessionTerminationReasons.GlobalSignOut, "subject-1", sessionId: null),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNeitherScope_DoesNothing()
    {
        // No setups: a strict-mock failure would mean the handler revoked grants.
        await Handler.HandleAsync(
            CreateCommand(
                SessionTerminationReasons.GlobalSignOut,
                subjectId: null,
                sessionId: null
            ),
            CancellationToken.None
        );
    }
}
