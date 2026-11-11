// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ChallengeCommand"/> message.
/// </summary>
internal class DefaultChallengeHandler
    : ICommandResponseHandler<ChallengeCommand, ReadOnlyEndpointDisposition>
{
    private bool DefaultChallengeSchemeFetched { get; set; }
    private string? DefaultChallengeSchemeName { get; set; }

    internal virtual ReadOnlyEndpointDisposition Handled() => ReadOnlyEndpointDisposition.Handled();

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> HandleAsync(
        ChallengeCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, _, authenticationProperties) = command;

        var httpContext = openIdContext.Http;

        // the default is "Identity.Application" which is compatible with Microsoft.AspNetCore.Identity
        // this scheme returns the local application user identity but still allows SSO logins from external identity providers
        // do not use the "Identity.External" scheme as it is only for external identity providers
        var challengeSchemeName = openIdClient.Settings.GetValue(
            OpenIdSettingKeys.AuthorizationChallengeScheme
        );
        if (challengeSchemeName == string.Empty)
        {
            challengeSchemeName = null;
        }

        await httpContext.ChallengeAsync(challengeSchemeName, authenticationProperties);

        return Handled();
    }
}
