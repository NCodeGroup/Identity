#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthorizeSubjectCommand"/> message.
/// </summary>
[PublicAPI]
public class DefaultAuthorizeSubjectHandler(
    ILogger<DefaultAuthorizeSubjectHandler> logger,
    TimeProvider timeProvider,
    IOpenIdErrorFactory errorFactory
) : ICommandResponseHandler<AuthorizeSubjectCommand, AuthorizeSubjectDisposition>
{
    private ILogger<DefaultAuthorizeSubjectHandler> Logger { get; } = logger;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    internal virtual AuthorizeSubjectDisposition Failed(IOpenIdError error) => new(error);
    internal virtual AuthorizeSubjectDisposition Authorized() => new(RequiresChallenge: false);
    internal virtual AuthorizeSubjectDisposition RequiresChallenge() => new(RequiresChallenge: true);

    /// <inheritdoc />
    public async ValueTask<AuthorizeSubjectDisposition> HandleAsync(
        AuthorizeSubjectCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest, authenticationTicket) = command;

        var clientSettings = openIdClient.Settings;
        var promptTypes = authorizationRequest.PromptTypes;

        if (promptTypes.Contains(OpenIdConstants.PromptTypes.CreateAccount))
        {
            Logger.LogInformation("Client requested account creation.");
            return RequiresChallenge();
        }

        var reAuthenticate =
            promptTypes.Contains(OpenIdConstants.PromptTypes.Login) ||
            promptTypes.Contains(OpenIdConstants.PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            Logger.LogInformation("Client requested re-authentication.");
            return RequiresChallenge();
        }

        var isAuthenticated = authenticationTicket.Subject.Identities.All(identity => identity.IsAuthenticated);
        if (!isAuthenticated)
        {
            if (promptTypes.Contains(OpenIdConstants.PromptTypes.None))
            {
                return Failed(ErrorFactory.LoginRequired());
            }

            Logger.LogInformation("The end-user is not authenticated.");
            return RequiresChallenge();
        }

        var subjectIsActive = await GetSubjectIsActiveAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            authenticationTicket,
            cancellationToken);

        if (!subjectIsActive)
        {
            if (promptTypes.Contains(OpenIdConstants.PromptTypes.None))
            {
                return Failed(ErrorFactory.LoginRequired());
            }

            Logger.LogInformation("The end-user is not active.");
            return RequiresChallenge();
        }

        // TODO: check that tenant from subject matches the current tenant

        // TODO: check that IdP from subject matches the request's ACR
        // example acr value: urn:ncode:oidc:idp:google

        // TODO: check consent

        // check MaxAge
        if (!ValidateMaxAge(authenticationTicket.Subject, authorizationRequest.MaxAge, clientSettings.ClockSkew))
        {
            if (promptTypes.Contains(OpenIdConstants.PromptTypes.None))
            {
                return Failed(ErrorFactory.LoginRequired());
            }

            Logger.LogInformation("MaxAge exceeded.");
            return RequiresChallenge();
        }

        // TODO: check local idp restrictions

        // TODO: check external idp restrictions

        // TODO: check client's user SSO timeout

        return Authorized();
    }

    private static async ValueTask<bool> GetSubjectIsActiveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdRequest openIdRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var result = new PositiveIsActiveResult();

        var command = new ValidateSubjectIsActiveCommand(
            openIdContext,
            openIdClient,
            openIdRequest,
            subjectAuthentication,
            result);

        var mediator = openIdContext.Mediator;
        await mediator.SendAsync(command, cancellationToken);

        return result.IsActive;
    }

    private bool ValidateMaxAge(ClaimsPrincipal subject, TimeSpan? maxAge, TimeSpan clockSkew)
    {
        /*
         * max_age
         * OPTIONAL. Maximum Authentication Age. Specifies the allowable elapsed time in seconds since the last time the
         * End-User was actively authenticated by the OP. If the elapsed time is greater than this value, the OP MUST attempt
         * to actively re-authenticate the End-User. (The max_age request parameter corresponds to the OpenID 2.0 PAPE
         * [OpenID.PAPE] max_auth_age request parameter.) When max_age is used, the ID Token returned MUST include an auth_time
         * Claim Value.
         */

        if (!maxAge.HasValue)
        {
            return true;
        }

        var authTimeClaim = subject.FindFirst(JoseClaimNames.Payload.AuthTime);
        if (authTimeClaim is null || !long.TryParse(authTimeClaim.Value, out var authTimeSeconds))
        {
            return false;
        }

        // use 'long/ticks' vs 'DateTime/DateTimeOffset' comparisons to avoid overflow exceptions

        var nowTicks = TimeProvider.GetTimestampWithPrecisionInSeconds();

        var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds);
        var authTimeTicks = authTime.UtcTicks;

        var maxAgeTicks = maxAge.Value.Ticks;
        var clockSkewTicks = clockSkew.Ticks;

        return nowTicks <= authTimeTicks + maxAgeTicks + clockSkewTicks;
    }
}
