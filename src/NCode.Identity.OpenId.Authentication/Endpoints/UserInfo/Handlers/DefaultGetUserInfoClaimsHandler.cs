// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="GetUserInfoClaimsCommand"/> message that supplies
/// the required <c>sub</c> claim. This off-the-shelf handler returns only the subject identifier; applications register
/// additional <see cref="ICommandHandler{GetUserInfoClaimsCommand}"/> handlers to contribute profile, email, and other
/// claims from their own user store (typically gated by the granted scopes).
/// </summary>
internal class DefaultGetUserInfoClaimsHandler
    : ICommandHandler<GetUserInfoClaimsCommand>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetUserInfoClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, subjectAuthentication, claims) = command;

        // The 'sub' claim is authoritative and always sourced from the authenticated subject.
        claims[JoseClaimNames.Payload.Sub] = subjectAuthentication.PrincipalId;

        return ValueTask.CompletedTask;
    }
}
