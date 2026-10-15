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
