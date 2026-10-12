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

using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Handles <see cref="OnEndSessionCommand"/> by performing the server-side termination: revoking the terminating
/// session's grants (which includes the session grant itself, since it carries its own <c>sid</c>) so other sessions
/// the subject holds are left intact — RP-initiated single-logout semantics. When no session is resolved the
/// revocation falls back to the subject's grants. Runs at high priority so revocation happens before notification
/// side effects.
/// </summary>
internal class DefaultRevokeGrantsOnEndSessionHandler(
    TimeProvider timeProvider,
    IStoreManagerFactory storeManagerFactory
) : ICommandHandler<OnEndSessionCommand>, ISupportMediatorPriority
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        OnEndSessionCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, subject) = command;
        var revokedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        // Prefer session-scoped revocation: a logout terminates the identified session only, leaving the subject's
        // other sessions (and their refresh tokens) valid. A subject-wide ("sign out everywhere") revocation is the
        // deliberate opt-in offered by the management bulk-revoke surface (ADR-0022), not the default on logout.
        string? subjectId = null;
        var sessionId = subject.SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            // No session was resolved (for example a non-interactive id_token_hint without a sid); fall back to the
            // subject's grants so the logout still invalidates server-side state.
            subjectId = subject.SubjectId;
            if (string.IsNullOrEmpty(subjectId))
            {
                return;
            }
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var grantStore = storeManager.GetStore<IGrantStore>();
        await grantStore.RevokeWhereAsync(
            subjectId,
            clientId: null,
            sessionId,
            revokedWhen,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);
    }
}
