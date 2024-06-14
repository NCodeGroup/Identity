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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Logic.Authorization;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthorizeCommand"/> message.
/// </summary>
public class DefaultAuthorizeHandler(
    ILogger<DefaultAuthorizeHandler> logger,
    TimeProvider timeProvider,
    IOpenIdErrorFactory errorFactory,
    IContinueService continueService,
    IAuthorizationInteractionService interactionService
) : ICommandResponseHandler<AuthorizeCommand, IResult?>
{
    private ILogger<DefaultAuthorizeHandler> Logger { get; } = logger;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IAuthorizationInteractionService InteractionService { get; } = interactionService;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IContinueService ContinueService { get; } = continueService;

    /// <inheritdoc />
    public async ValueTask<IResult?> HandleAsync(
        AuthorizeCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest, authenticationTicket) = command;

        var clientSettings = openIdClient.Settings;
        var isContinuation = authorizationRequest.IsContinuation;

        // prevent infinite loops from continuations and user interaction
        var effectivePromptTypes = isContinuation ?
            [OpenIdConstants.PromptTypes.None] :
            authorizationRequest.PromptTypes;

        if (effectivePromptTypes.Contains(OpenIdConstants.PromptTypes.CreateAccount))
        {
            Logger.LogInformation("Client requested account creation.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetCreateAccountUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        var reAuthenticate =
            effectivePromptTypes.Contains(OpenIdConstants.PromptTypes.Login) ||
            effectivePromptTypes.Contains(OpenIdConstants.PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            Logger.LogInformation("Client requested re-authentication.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        if (authenticationTicket is not { Subject.IsAuthenticated: true })
        {
            if (effectivePromptTypes.Contains(OpenIdConstants.PromptTypes.None))
            {
                var error = ErrorFactory.LoginRequired();
                var redirectUri = authorizationRequest.RedirectUri;
                var responseMode = authorizationRequest.ResponseMode;
                return new AuthorizationResult(redirectUri, responseMode, error);
            }

            Logger.LogInformation("The user is not authenticated.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        if (!await ValidateSubjectIsActiveAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket,
                cancellationToken))
        {
            if (effectivePromptTypes.Contains(OpenIdConstants.PromptTypes.None))
            {
                var error = ErrorFactory.LoginRequired();
                var redirectUri = authorizationRequest.RedirectUri;
                var responseMode = authorizationRequest.ResponseMode;
                return new AuthorizationResult(redirectUri, responseMode, error);
            }

            Logger.LogInformation("The user is not active.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        // TODO: check that tenant from subject matches the current tenant

        // TODO: check that IdP from subject matches the request's ACR
        // example acr value: urn:ncode:oidc:idp:google

        // TODO: check consent

        // check MaxAge
        if (!ValidateMaxAge(authenticationTicket.Subject, authorizationRequest.MaxAge, clientSettings.ClockSkew))
        {
            if (effectivePromptTypes.Contains(OpenIdConstants.PromptTypes.None))
            {
                var error = ErrorFactory.LoginRequired();
                var redirectUri = authorizationRequest.RedirectUri;
                var responseMode = authorizationRequest.ResponseMode;
                return new AuthorizationResult(redirectUri, responseMode, error);
            }

            Logger.LogInformation("MaxAge exceeded.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        // TODO: check local idp restrictions

        // TODO: check external idp restrictions

        // TODO: check client's user SSO timeout

        return null;
    }

    private static async ValueTask<bool> ValidateSubjectIsActiveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdMessage openIdMessage,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var result = new ValidateSubjectIsActiveResult();

        var command = new ValidateSubjectIsActiveCommand(
            openIdContext,
            openIdClient,
            openIdMessage,
            subjectAuthentication,
            result);

        var mediator = openIdContext.Mediator;
        await mediator.SendAsync(command, cancellationToken);

        return result.IsActive;
    }

    private bool ValidateMaxAge(ClaimsIdentity subject, TimeSpan? maxAge, TimeSpan clockSkew)
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
