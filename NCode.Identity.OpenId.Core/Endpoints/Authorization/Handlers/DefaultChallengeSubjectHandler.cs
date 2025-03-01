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
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ChallengeSubjectCommand"/> message.
/// </summary>
[PublicAPI]
public class DefaultChallengeSubjectHandler : ICommandResponseHandler<ChallengeSubjectCommand, OperationDisposition>
{
    private bool DefaultChallengeSchemeFetched { get; set; }
    private string? DefaultChallengeSchemeName { get; set; }

    internal virtual OperationDisposition Handled() => new(WasHandled: true);

    /// <inheritdoc />
    public async ValueTask<OperationDisposition> HandleAsync(
        ChallengeSubjectCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, _, authenticationProperties) = command;

        var httpContext = openIdContext.Http;

        // the default is "Identity.Application" which is compatible with Microsoft.AspNetCore.Identity
        // this scheme returns the local application user identity but still allows SSO logins from external identity providers
        // do not use the "Identity.External" scheme as it is only for external identity providers
        var challengeSchemeName = openIdClient.Settings.GetValue(SettingKeys.AuthorizationChallengeScheme);
        if (challengeSchemeName == string.Empty)
        {
            challengeSchemeName = null;
        }

        await httpContext.ChallengeAsync(challengeSchemeName, authenticationProperties);

        return Handled();
    }
}
