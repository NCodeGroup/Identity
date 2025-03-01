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
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Settings;
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
    internal virtual AuthorizeSubjectDisposition Authorized() => new(ChallengeRequired: false);
    internal virtual AuthorizeSubjectDisposition ChallengeRequired() => new(ChallengeRequired: true);
    internal virtual AuthorizeSubjectDisposition LoginRequired() => Failed(ErrorFactory.LoginRequired());
    internal virtual AuthorizeSubjectDisposition InteractionRequired(bool noPrompt) => noPrompt ? LoginRequired() : ChallengeRequired();

    /// <inheritdoc />
    public async ValueTask<AuthorizeSubjectDisposition> HandleAsync(
        AuthorizeSubjectCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest, authenticationTicket) = command;

        var tenant = openIdContext.Tenant;
        var clientSettings = openIdClient.Settings;
        var promptTypes = authorizationRequest.PromptTypes;

        if (promptTypes.Contains(OpenIdConstants.PromptTypes.CreateAccount))
        {
            Logger.LogInformation("Client requested account creation.");
            return ChallengeRequired();
        }

        var reAuthenticate =
            promptTypes.Contains(OpenIdConstants.PromptTypes.Login) ||
            promptTypes.Contains(OpenIdConstants.PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            Logger.LogInformation("Client requested re-authentication.");
            return ChallengeRequired();
        }

        var noPrompt = promptTypes.Contains(OpenIdConstants.PromptTypes.None);

        // check that subject is authenticated
        var isAuthenticated = authenticationTicket.Subject.Identities.All(identity => identity.IsAuthenticated);
        if (!isAuthenticated)
        {
            Logger.LogInformation("The end-user is not authenticated.");
            return InteractionRequired(noPrompt);
        }

        // check that subject is active
        var subjectIsActive = await GetSubjectIsActiveAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            authenticationTicket,
            cancellationToken);

        if (!subjectIsActive)
        {
            Logger.LogInformation("The end-user is not active.");
            return InteractionRequired(noPrompt);
        }

        // check TenantId
        var expectedTenantId = tenant.TenantId;
        var receivedTenantId = GetReceivedTenantId(authenticationTicket);
        if (!string.Equals(expectedTenantId, receivedTenantId, StringComparison.Ordinal))
        {
            Logger.LogInformation("The end-user's tenant does not match the current tenant.");
            return InteractionRequired(noPrompt);
        }

        // check requested IdP
        var receivedIdp = authenticationTicket.Subject.FindFirstValue(JoseClaimNames.Payload.Idp);
        if (!IsRequestedIdpValid(receivedIdp, authorizationRequest))
        {
            Logger.LogInformation("The end-user's IdP does not match the requested IdP.");
            return InteractionRequired(noPrompt);
        }

        // check allowed IdP
        if (!IsReceivedIdpAllowed(receivedIdp, clientSettings))
        {
            Logger.LogInformation("The end-user's IdP is not allowed according to the client's settings.");
            return InteractionRequired(noPrompt);
        }

        var clockSkew = clientSettings.GetValue(SettingKeys.ClockSkew);

        // check the request's MaxAge
        var authTime = GetAuthTime(authenticationTicket.Subject);
        if (!ValidateMaxAge(authTime, authorizationRequest.MaxAge, clockSkew))
        {
            Logger.LogInformation("The end-user's authentication time is too old from the request's MaxAge.");
            return InteractionRequired(noPrompt);
        }

        // check the client's MaxAge
        if (!ValidateMaxAge(authTime, clientSettings.GetValue(SettingKeys.SubjectMaxAge), clockSkew))
        {
            Logger.LogInformation("The end-user's authentication time is too old from the client's MaxAge.");
            return InteractionRequired(noPrompt);
        }

        // TODO: check consent

        return Authorized();
    }

    private static bool IsReceivedIdpAllowed(string? receivedIdp, IReadOnlySettingCollection settings)
    {
        var allowed = settings.GetValue(SettingKeys.AllowedIdentityProviders);
        return allowed.Count == 0 || allowed.Contains(receivedIdp, StringComparer.Ordinal);
    }

    private static bool IsRequestedIdpValid(string? receivedIdp, IAuthorizationRequest authorizationRequest)
    {
        // if no specific acr values were requested, then any idp is valid
        var acrValues = authorizationRequest.AcrValues;
        if (acrValues.Count == 0)
        {
            return true;
        }

        // TODO: move this constant somewhere else
        const string prefix = "idp:";

        var anyRequested = false;
        var requestedIdpValues = acrValues
            .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value[prefix.Length..]);

        foreach (var requestedIdp in requestedIdpValues)
        {
            // if the received idp matches any requested idp, then the idp is valid
            if (string.Equals(requestedIdp, receivedIdp, StringComparison.Ordinal))
            {
                return true;
            }

            anyRequested = true;
        }

        // if no specific idp values were requested, then any idp is valid
        return !anyRequested;
    }

    private static string? GetReceivedTenantId(SubjectAuthentication subjectAuthentication)
    {
        // when we challenge, we store the tenant id in the authentication properties
        var tenantId = subjectAuthentication.AuthenticationProperties.GetString(OpenIdConstants.AuthenticationPropertyItems.TenantId);
        return !string.IsNullOrEmpty(tenantId) ? tenantId : subjectAuthentication.Subject.FindFirstValue(JoseClaimNames.Payload.Tid);
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

    private static DateTimeOffset? GetAuthTime(ClaimsPrincipal subject)
    {
        var authTimeClaim = subject.FindFirst(JoseClaimNames.Payload.AuthTime);
        if (authTimeClaim is null || !long.TryParse(authTimeClaim.Value, out var authTimeSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds);
    }

    private bool ValidateMaxAge(DateTimeOffset? authTime, TimeSpan? maxAge, TimeSpan clockSkew)
    {
        /*
         * max_age
         * OPTIONAL. Maximum Authentication Age. Specifies the allowable elapsed time in seconds since the last time the
         * End-User was actively authenticated by the OP. If the elapsed time is greater than this value, the OP MUST attempt
         * to actively re-authenticate the End-User. (The max_age request parameter corresponds to the OpenID 2.0 PAPE
         * [OpenID.PAPE] max_auth_age request parameter.) When max_age is used, the ID Token returned MUST include an auth_time
         * Claim Value.
         */

        if (!maxAge.HasValue || maxAge == TimeSpan.MaxValue)
        {
            return true;
        }

        if (!authTime.HasValue)
        {
            return false;
        }

        // use 'long/ticks' vs 'DateTime/DateTimeOffset' comparisons to avoid overflow exceptions

        var nowTicks = TimeProvider.GetTimestampWithPrecisionInSeconds();
        var authTimeTicks = authTime.Value.UtcTicks;
        var maxAgeTicks = maxAge.Value.Ticks;
        var clockSkewTicks = clockSkew.Ticks;

        return nowTicks <= authTimeTicks + maxAgeTicks + clockSkewTicks;
    }
}
