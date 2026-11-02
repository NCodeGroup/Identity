// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultAuditSessionTerminatedHandlerTests : BaseTests
{
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private DefaultAuditSessionTerminatedHandler Handler { get; }

    public DefaultAuditSessionTerminatedHandlerTests()
    {
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        Handler = new DefaultAuditSessionTerminatedHandler(MockAuditEventRecorder.Object);
    }

    [Fact]
    public async Task HandleAsync_RecordsTerminationAudit()
    {
        var context = Mock.Of<OpenIdContext>();
        var command = new OnSessionTerminatedCommand(
            context,
            new SessionTermination(SessionTerminationReasons.IdleTimeout, "subject-1", "sid-1", [])
        );

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordSessionTerminatedAsync(
                    context,
                    SessionTerminationReasons.IdleTimeout,
                    "subject-1",
                    "sid-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }
}
