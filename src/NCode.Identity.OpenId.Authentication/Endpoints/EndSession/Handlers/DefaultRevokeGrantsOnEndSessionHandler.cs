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
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Handles <see cref="OnEndSessionCommand"/> by performing the server-side termination: revoking the subject's grants
/// (which includes the session grant), or revoking the session directly when the subject is unknown. Runs at high
/// priority so revocation happens before notification side effects.
/// </summary>
internal class DefaultRevokeGrantsOnEndSessionHandler(
    TimeProvider timeProvider,
    IStoreManagerFactory storeManagerFactory,
    ISessionService sessionService
) : ICommandHandler<OnEndSessionCommand>, ISupportMediatorPriority
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ISessionService SessionService { get; } = sessionService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        OnEndSessionCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, subject) = command;
        var revokedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        if (!string.IsNullOrEmpty(subject.SubjectId))
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var grantStore = storeManager.GetStore<IGrantStore>();
            await grantStore.RevokeWhereAsync(
                subject.SubjectId,
                clientId: null,
                revokedWhen,
                cancellationToken
            );
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        else if (!string.IsNullOrEmpty(subject.SessionId))
        {
            await SessionService.RevokeAsync(
                openIdContext,
                subject.SessionId,
                revokedWhen,
                cancellationToken
            );
        }
    }
}
