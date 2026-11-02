// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Handles <see cref="OnSessionTerminatedCommand"/> by recording a session-termination audit event.
/// </summary>
internal class DefaultAuditSessionTerminatedHandler(IAuditEventRecorder auditEventRecorder)
    : ICommandHandler<OnSessionTerminatedCommand>
{
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        OnSessionTerminatedCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, termination) = command;

        return AuditEventRecorder.RecordSessionTerminatedAsync(
            openIdContext,
            termination.Reason,
            termination.SubjectId,
            termination.SessionId,
            cancellationToken
        );
    }
}
