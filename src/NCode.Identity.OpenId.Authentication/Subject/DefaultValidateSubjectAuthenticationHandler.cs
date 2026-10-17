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

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Messages.Parameters;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Json;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateSubjectAuthenticationCommand"/> message.
/// </summary>
/// <remarks>
/// The application should also register an additional handler to validate the subject's active status.
/// </remarks>
internal class DefaultValidateSubjectAuthenticationHandler(
    TimeProvider timeProvider,
    ILogger<DefaultValidateSubjectAuthenticationHandler> logger
) : ICommandHandler<ValidateSubjectAuthenticationCommand>, ISupportMediatorPriority
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<DefaultValidateSubjectAuthenticationHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "InvertIf")]
    public ValueTask HandleAsync(
        ValidateSubjectAuthenticationCommand command,
        CancellationToken cancellationToken
    )
    {
        var (
            openIdContext,
            openIdClient,
            openIdRequest,
            subjectAuthentication,
            operationDisposition,
            enforceAuthenticationAge
        ) = command;

        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        // short-circuit if we already have an error
        if (operationDisposition.HasError)
        {
            return ValueTask.CompletedTask;
        }

        // verify the subject's TenantId
        var tenant = openIdContext.Tenant;
        var expectedTenantId = tenant.TenantId;
        var receivedTenantId = GetReceivedTenantId(subjectAuthentication);
        if (!string.Equals(expectedTenantId, receivedTenantId, StringComparison.Ordinal))
        {
            const string message = "The end-user's tenant does not match the current tenant.";
            operationDisposition.Error ??= errorFactory.AccessDenied(message);
            Logger.SubjectValidationFailed(message);
            return ValueTask.CompletedTask;
        }

        // verify the subject is authenticated
        var isAuthenticated = subjectAuthentication.Subject.Identities.All(identity =>
            identity.IsAuthenticated
        );
        if (!isAuthenticated)
        {
            const string message = "The end-user is not authenticated.";
            operationDisposition.Error ??= errorFactory.AccessDenied(message);
            Logger.SubjectValidationFailed(message);
            return ValueTask.CompletedTask;
        }

        var authTime = GetAuthTime(subjectAuthentication.Subject);

        // Authentication-age freshness (the request's max_age and the client's subject_max_age) is an interactive
        // authorization-time concern: it decides whether to force re-authentication before issuing a code. It is
        // enforced at the authorization endpoint and skipped when a back-channel token grant re-validates the subject
        // against a later clock (re-checking the fixed auth_time against "now" would wrongly reject valid code/refresh
        // exchanges that occur after the window elapses).
        if (enforceAuthenticationAge)
        {
            // verify the request's max age
            var requestMaxAge = openIdRequest.Parameters.GetValueOrDefault(
                OpenIdCommonParameters.MaxAge
            );
            if (!ValidateMaxAge(authTime, requestMaxAge))
            {
                const string message =
                    "The end-user's authentication time is too old from the request's MaxAge.";
                operationDisposition.Error ??= errorFactory.AccessDenied(message);
                Logger.SubjectValidationFailed(message);
                return ValueTask.CompletedTask;
            }

            // verify the client's max age (an unset subject_max_age imposes no freshness constraint)
            var clientMaxAge = settings.GetValueOrNull(OpenIdSettingKeys.SubjectMaxAge);
            if (!ValidateMaxAge(authTime, clientMaxAge))
            {
                const string message =
                    "The end-user's authentication time is too old from the client's MaxAge.";
                operationDisposition.Error ??= errorFactory.AccessDenied(message);
                Logger.SubjectValidationFailed(message);
                return ValueTask.CompletedTask;
            }
        }

        // check requested IdP
        var receivedIdp = subjectAuthentication.Subject.FindFirstValue(JoseClaimNames.Payload.Idp);
        if (!IsRequestedIdpValid(receivedIdp, openIdRequest))
        {
            const string message = "The end-user's IdP does not match the requested IdP.";
            operationDisposition.Error ??= errorFactory.AccessDenied(message);
            Logger.SubjectValidationFailed(message);
            return ValueTask.CompletedTask;
        }

        // check allowed IdP
        if (!IsReceivedIdpAllowed(receivedIdp, settings))
        {
            const string message =
                "The end-user's IdP is not allowed according to the client's settings.";
            operationDisposition.Error ??= errorFactory.AccessDenied(message);
            Logger.SubjectValidationFailed(message);
            return ValueTask.CompletedTask;
        }

        // enforce a requested essential acr
        if (!IsRequestedAcrSatisfied(subjectAuthentication.Subject, openIdRequest))
        {
            const string message =
                "The end-user's acr does not satisfy the requested essential acr values.";
            operationDisposition.Error ??= errorFactory.AccessDenied(message);
            Logger.SubjectValidationFailed(message);
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsRequestedAcrSatisfied(
        ClaimsPrincipal subject,
        IOpenIdRequest openIdRequest
    )
    {
        // Per OpenID Connect Core 1.0 §5.5.1.1, acr is only a hard requirement when it is requested as an *essential*
        // id_token.acr claim with specific values; the issued acr MUST then match one of them. A voluntary request
        // (including the entire `acr_values` parameter, which the spec defines as voluntary) is best-effort and imposes
        // no constraint here.
        var requestClaims = openIdRequest.Parameters.GetValueOrDefault(
            OpenIdAuthenticationParameters.Claims
        );
        if (
            requestClaims?.IdToken is not { } idTokenClaims
            || !idTokenClaims.TryGetValue(JoseClaimNames.Payload.Acr, out var acrRequest)
            || acrRequest is not { Essential: true }
        )
        {
            return true;
        }

        var requestedValues = GetRequestedAcrValues(acrRequest);
        if (requestedValues.Count == 0)
        {
            // An essential acr request without specific values only asks that acr be returned, not a particular value;
            // emission is handled by the token pipeline, so there is nothing to enforce.
            return true;
        }

        var satisfiedAcr = subject.FindFirstValue(JoseClaimNames.Payload.Acr);
        return satisfiedAcr is not null && requestedValues.Contains(satisfiedAcr);
    }

    private static HashSet<string> GetRequestedAcrValues(IRequestClaim acrRequest)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(acrRequest.Value))
        {
            values.Add(acrRequest.Value);
        }

        if (acrRequest.Values is not null)
        {
            foreach (var value in acrRequest.Values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    private static bool IsReceivedIdpAllowed(
        string? receivedIdp,
        IReadOnlySettingCollection settings
    )
    {
        // An unset allowlist imposes no restriction (ADR-0010); a set allowlist (which merges as a
        // ceiling via intersection) is explicit, so a narrowed-to-empty list denies every IdP.
        if (!settings.TryGetValue(OpenIdSettingKeys.AllowedIdentityProviders, out var allowed))
            return true;
        return allowed.Contains(receivedIdp, StringComparer.Ordinal);
    }

    private static bool IsRequestedIdpValid(string? receivedIdp, IOpenIdRequest openIdRequest)
    {
        // if no specific acr values were requested, then any idp is valid
        var acrValues = openIdRequest.Parameters.GetValueOrDefault(
            OpenIdCommonParameters.AcrValues
        );
        if (acrValues is null || acrValues.Count == 0)
        {
            return true;
        }

        const string prefix = OpenIdConstants
            .AuthenticationContextClassReferencePrefixes
            .IdentityProvider;

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
        var tenantId = subjectAuthentication.AuthenticationProperties.GetString(
            OpenIdConstants.AuthenticationPropertyItems.TenantId
        );
        return !string.IsNullOrEmpty(tenantId)
            ? tenantId
            : subjectAuthentication.Subject.FindFirstValue(JoseClaimNames.Payload.Tid);
    }

    private static DateTimeOffset? GetAuthTime(ClaimsPrincipal subject)
    {
        var authTimeValue = subject.FindFirstValue(JoseClaimNames.Payload.AuthTime);
        return DateCoercion.TryParseNumericDate(authTimeValue, out var authTime) ? authTime : null;
    }

    private bool ValidateMaxAge(DateTimeOffset? authTime, TimeSpan? maxAge)
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

        // max_age is the elapsed time since the OP last authenticated the subject: a pure OP-internal comparison of the
        // OP-issued auth_time against the OP clock. No clock_skew leeway is applied (that setting is for validating token
        // time claims across parties) — adding it would widen the window and let a small max_age (e.g. max_age=1) never
        // force re-authentication. Compare via long/ticks to avoid DateTime overflow.

        var nowTicks = TimeProvider.GetTimestampWithPrecisionInSeconds();
        var authTimeTicks = authTime.Value.UtcTicks;
        var maxAgeTicks = maxAge.Value.Ticks;

        return nowTicks <= authTimeTicks + maxAgeTicks;
    }
}
