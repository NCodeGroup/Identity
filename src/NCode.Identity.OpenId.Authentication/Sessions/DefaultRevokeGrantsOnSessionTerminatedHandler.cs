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

using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Handles <see cref="OnSessionTerminatedCommand"/> by revoking the terminated session's grants (which includes the
/// session grant itself, since it carries its own <c>sid</c>), or every grant the subject holds when the termination
/// is subject-wide (such as a global sign-out). The revocation reads the grants again and retries when it loses a
/// concurrency race. Runs at high priority so revocation happens before notification side effects.
/// </summary>
internal class DefaultRevokeGrantsOnSessionTerminatedHandler(
    TimeProvider timeProvider,
    IPersistedGrantService persistedGrantService
) : ICommandHandler<OnSessionTerminatedCommand>, ISupportMediatorPriority
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        OnSessionTerminatedCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, termination) = command;

        // Session-scoped when a sid is present (leaving the subject's other sessions intact); otherwise subject-wide.
        GrantFilter filter;
        if (!string.IsNullOrEmpty(termination.SessionId))
        {
            filter = new GrantFilter { SessionId = termination.SessionId };
        }
        else if (!string.IsNullOrEmpty(termination.SubjectId))
        {
            filter = new GrantFilter { SubjectId = termination.SubjectId };
        }
        else
        {
            return;
        }

        var revokedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        await PersistedGrantService.RevokeWhereAsync(
            openIdContext,
            filter,
            revokedWhen,
            cancellationToken
        );
    }
}
