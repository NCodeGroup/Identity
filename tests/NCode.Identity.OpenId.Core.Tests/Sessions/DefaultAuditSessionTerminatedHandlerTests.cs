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
            new SessionTermination(SessionTerminationReason.IdleTimeout, "subject-1", "sid-1", [])
        );

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordSessionTerminatedAsync(
                    context,
                    SessionTerminationReason.IdleTimeout,
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
