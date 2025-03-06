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
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Subject;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateSubjectCommand"/> message.
/// </summary>
/// <remarks>
/// The application should also register an additional handler to validate the subject's active status.
/// </remarks>
[PublicAPI]
public class DefaultValidateSubjectHandler(
    TimeProvider timeProvider,
    ILogger<DefaultValidateSubjectHandler> logger,
    IOpenIdErrorFactory errorFactory
) : ICommandHandler<ValidateSubjectCommand>, ISupportMediatorPriority
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<DefaultValidateSubjectHandler> Logger { get; } = logger;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriorityHigh;

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "InvertIf")]
    public ValueTask HandleAsync(
        ValidateSubjectCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, openIdRequest, subjectAuthentication, operationDisposition) = command;
        var settings = openIdClient.Settings;

        // short-circuit if we already have an error
        if (operationDisposition.HasOpenIdError)
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
            operationDisposition.OpenIdError ??= ErrorFactory.AccessDenied(message);
            Logger.LogWarning(message);
            return ValueTask.CompletedTask;
        }

        // verify the subject is authenticated
        var isAuthenticated = subjectAuthentication.Subject.Identities.All(identity => identity.IsAuthenticated);
        if (!isAuthenticated)
        {
            const string message = "The end-user is not authenticated.";
            operationDisposition.OpenIdError ??= ErrorFactory.AccessDenied(message);
            Logger.LogWarning(message);
            return ValueTask.CompletedTask;
        }

        var clockSkew = settings.GetValue(SettingKeys.ClockSkew);
        var authTime = GetAuthTime(subjectAuthentication.Subject);

        // verify the request's max age
        var requestMaxAge = openIdRequest.Parameters.GetValueOrDefault(KnownParameters.MaxAge);
        if (!ValidateMaxAge(authTime, requestMaxAge, clockSkew))
        {
            const string message = "The end-user's authentication time is too old from the request's MaxAge.";
            operationDisposition.OpenIdError ??= ErrorFactory.AccessDenied(message);
            Logger.LogWarning(message);
            return ValueTask.CompletedTask;
        }

        // verify the client's max age
        var clientMaxAge = settings.GetValue(SettingKeys.SubjectMaxAge);
        if (!ValidateMaxAge(authTime, clientMaxAge, clockSkew))
        {
            const string message = "The end-user's authentication time is too old from the client's MaxAge.";
            operationDisposition.OpenIdError ??= ErrorFactory.AccessDenied(message);
            Logger.LogWarning(message);
            return ValueTask.CompletedTask;
        }

        // check requested IdP
        var receivedIdp = subjectAuthentication.Subject.FindFirstValue(JoseClaimNames.Payload.Idp);
        if (!IsRequestedIdpValid(receivedIdp, openIdRequest))
        {
            const string message = "The end-user's IdP does not match the requested IdP.";
            operationDisposition.OpenIdError ??= ErrorFactory.AccessDenied(message);
            Logger.LogWarning(message);
            return ValueTask.CompletedTask;
        }

        // check allowed IdP
        if (!IsReceivedIdpAllowed(receivedIdp, settings))
        {
            const string message = "The end-user's IdP is not allowed according to the client's settings.";
            operationDisposition.OpenIdError ??= ErrorFactory.AccessDenied(message);
            Logger.LogWarning(message);
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsReceivedIdpAllowed(string? receivedIdp, IReadOnlySettingCollection settings)
    {
        var allowed = settings.GetValue(SettingKeys.AllowedIdentityProviders);
        return allowed.Count == 0 || allowed.Contains(receivedIdp, StringComparer.Ordinal);
    }

    private static bool IsRequestedIdpValid(string? receivedIdp, IOpenIdRequest openIdRequest)
    {
        // if no specific acr values were requested, then any idp is valid
        var acrValues = openIdRequest.Parameters.GetValueOrDefault(KnownParameters.AcrValues);
        if (acrValues is null || acrValues.Count == 0)
        {
            return true;
        }

        const string prefix = OpenIdConstants.AuthenticationContextClassReferencePrefixes.IdentityProvider;

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
