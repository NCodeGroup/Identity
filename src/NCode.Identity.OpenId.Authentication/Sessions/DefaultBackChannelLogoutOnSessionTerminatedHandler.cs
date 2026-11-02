// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
