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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Handles <see cref="OnSessionTerminatedCommand"/> by sending a back-channel logout notification to each relying party
/// that participated in the terminated session. Runs at low priority so it follows the server-side revocation.
/// </summary>
internal class DefaultBackChannelLogoutOnSessionTerminatedHandler(
    IOpenIdClientResolver clientResolver,
    IBackChannelLogoutNotifier backChannelLogoutNotifier
) : ICommandHandler<OnSessionTerminatedCommand>, ISupportMediatorPriority
{
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;
    private IBackChannelLogoutNotifier BackChannelLogoutNotifier { get; } =
        backChannelLogoutNotifier;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        OnSessionTerminatedCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, termination) = command;

        foreach (var clientId in termination.ParticipatingClientIds)
        {
            var client = await ClientResolver.ResolveClientOrDefaultAsync(
                openIdContext,
                clientId,
                cancellationToken
            );
            if (client is null)
            {
                continue;
            }

            await BackChannelLogoutNotifier.NotifyAsync(
                openIdContext,
                client,
                termination.SubjectId,
                termination.SessionId,
                cancellationToken
            );
        }
    }
}
