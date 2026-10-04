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

using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Audit;
using NCode.Identity.Events.Logging;
using Xunit;

namespace NCode.Identity.Events;

public class DefaultAuditLoggingHandlerTests
{
    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenOutcomeSuccess_LogsAuditSucceededAtInformation()
    {
        var logger = new ListLogger<DefaultAuditLoggingHandler>();
        var handler = new DefaultAuditLoggingHandler(logger);

        await handler.HandleAsync(
            new TestAuditEvent { Outcome = AuditOutcome.Success },
            CancellationToken.None
        );

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(EventIds.AuditSucceeded, entry.EventId);
    }

    [Fact]
    public async Task HandleAsync_WhenOutcomeFailure_LogsAuditFailedAtWarning()
    {
        var logger = new ListLogger<DefaultAuditLoggingHandler>();
        var handler = new DefaultAuditLoggingHandler(logger);

        await handler.HandleAsync(
            new TestAuditEvent { Outcome = AuditOutcome.Failure },
            CancellationToken.None
        );

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(EventIds.AuditFailed, entry.EventId);
    }

    [Fact]
    public async Task HandleAsync_WhenOutcomeIsCustomCode_LogsAuditRecordedAtInformation()
    {
        var logger = new ListLogger<DefaultAuditLoggingHandler>();
        var handler = new DefaultAuditLoggingHandler(logger);

        await handler.HandleAsync(
            new TestAuditEvent { Outcome = "denied" },
            CancellationToken.None
        );

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(EventIds.AuditRecorded, entry.EventId);
    }

    #endregion
}
