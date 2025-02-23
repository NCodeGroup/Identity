#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ChallengeAuthorizationCommand"/> message.
/// </summary>
[PublicAPI]
public class DefaultChallengeAuthorizationHandler(
    IAuthenticationSchemeProvider authenticationSchemeProvider
) : ICommandResponseHandler<ChallengeAuthorizationCommand, OperationDisposition>
{
    private IAuthenticationSchemeProvider AuthenticationSchemeProvider { get; } = authenticationSchemeProvider;

    private bool DefaultChallengeSchemeFetched { get; set; }
    private string? DefaultChallengeSchemeName { get; set; }

    internal virtual OperationDisposition Handled() => new(WasHandled: true);

    /// <inheritdoc />
    public async ValueTask<OperationDisposition> HandleAsync(
        ChallengeAuthorizationCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, _, authenticationProperties) = command;

        var httpContext = openIdContext.Http;

        var challengeSchemeName = openIdClient.Settings.AuthorizationChallengeScheme;
        if (string.IsNullOrEmpty(challengeSchemeName))
        {
            if (!DefaultChallengeSchemeFetched)
            {
                var challengeScheme = await AuthenticationSchemeProvider.GetDefaultChallengeSchemeAsync();
                DefaultChallengeSchemeName = challengeScheme?.Name;
                DefaultChallengeSchemeFetched = true;
            }

            challengeSchemeName = DefaultChallengeSchemeName;
        }

        await httpContext.ChallengeAsync(challengeSchemeName, authenticationProperties);

        return Handled();
    }
}
