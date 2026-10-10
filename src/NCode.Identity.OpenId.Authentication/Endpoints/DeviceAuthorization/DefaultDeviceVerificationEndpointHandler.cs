#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;

/// <summary>
/// Provides a default implementation of the user-facing device verification endpoint (RFC 8628 §3.3) where the end user
/// enters their <c>user_code</c> to approve or deny a pending device authorization request. The endpoint authenticates
/// the end user with the same subject-authentication seam the authorization endpoint uses; the presentation of the
/// confirmation page itself is the host's responsibility.
/// </summary>
internal class DefaultDeviceVerificationEndpointHandler(
    TimeProvider timeProvider,
    IPersistedGrantService persistedGrantService,
    IUserCodeGenerator userCodeGenerator
) : IEndpointProvider
{
    private const string ActionParameter = "action";
    private const string ApproveAction = "approve";
    private const string DenyAction = "deny";

    private TimeProvider TimeProvider { get; } = timeProvider;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IUserCodeGenerator UserCodeGenerator { get; } = userCodeGenerator;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods(
                OpenIdConstants.EndpointPaths.DeviceVerification,
                [HttpMethods.Get, HttpMethods.Post],
                HandleRouteAsync
            )
            .WithName(OpenIdConstants.EndpointNames.DeviceVerification)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .DisableAntiforgery();

    /// <summary>
    /// Device verification endpoint (OAuth 2.0 Device Authorization Grant, RFC 8628).
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    internal async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var mediator = openIdContext.Mediator;
        var errorFactory = openIdContext.ErrorFactory;
        var openIdEnvironment = openIdContext.Environment;

        var disposition = await mediator.SendAsync<
            AuthenticateSubjectCommand,
            AuthenticateSubjectDisposition
        >(new AuthenticateSubjectCommand(openIdContext), cancellationToken);

        if (disposition.HasError)
            return disposition.Error.WithStatusCode(StatusCodes.Status400BadRequest).AsHttpResult();

        if (!disposition.IsAuthenticated)
            return ChallengeForLogin(httpContext);

        var subjectAuthentication = disposition.Ticket.Value;

        var isPostVerb = httpContext.Request.Method == HttpMethods.Post;

        var userCodeRaw = isPostVerb
            ? httpContext.Request.Form[OpenIdConstants.Parameters.UserCode].ToString()
            : httpContext.Request.Query[OpenIdConstants.Parameters.UserCode].ToString();

        if (string.IsNullOrEmpty(userCodeRaw))
        {
            return errorFactory
                .InvalidRequest("The user_code is missing.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var tenantId = openIdContext.Tenant.TenantId;
        var userCodeNormalized = UserCodeGenerator.Normalize(userCodeRaw);

        var userCodeGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.DeviceUserCode,
            userCodeNormalized
        );

        var userCodePointer = await PersistedGrantService.GetOrDefaultAsync<DeviceUserCodeGrant>(
            openIdContext,
            userCodeGrantId,
            cancellationToken
        );

        if (
            !userCodePointer.HasValue
            || userCodePointer.Value.Status != PersistedGrantStatus.Active
        )
        {
            return errorFactory
                .InvalidRequest("The user_code is invalid, expired, or already used.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var deviceCode = userCodePointer.Value.Payload.DeviceCode;
        var deviceCodeGrantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.DeviceCode,
            deviceCode
        );

        var deviceCodeGrant = await PersistedGrantService.GetOrDefaultAsync<DeviceCodeGrant>(
            openIdContext,
            deviceCodeGrantId,
            cancellationToken
        );

        if (
            !deviceCodeGrant.HasValue
            || deviceCodeGrant.Value.Status != PersistedGrantStatus.Active
        )
        {
            return errorFactory
                .InvalidRequest("The device authorization request is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var payload = deviceCodeGrant.Value.Payload;

        // GET renders the confirmation; the host presents the client and scopes to the authenticated user.
        if (!isPostVerb)
        {
            return TypedResults.Json(
                new VerificationPrompt
                {
                    ClientId = payload.ClientId,
                    Scopes = payload.Scopes,
                    UserCode = userCodeRaw,
                    Status = payload.Status,
                },
                openIdEnvironment.JsonSerializerOptions
            );
        }

        var action = httpContext.Request.Form[ActionParameter].ToString();
        var approved = string.Equals(action, ApproveAction, StringComparison.OrdinalIgnoreCase);
        var denied = string.Equals(action, DenyAction, StringComparison.OrdinalIgnoreCase);

        if (!approved && !denied)
        {
            return errorFactory
                .InvalidRequest(
                    $"The '{ActionParameter}' must be '{ApproveAction}' or '{DenyAction}'."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        // Only act on a request that is still pending so a second submission is a no-op.
        if (payload.Status == DeviceAuthorizationStatus.Pending)
        {
            var updatedPayload = approved
                ? payload with
                {
                    Status = DeviceAuthorizationStatus.Approved,
                    SubjectAuthentication = subjectAuthentication,
                }
                : payload with
                {
                    Status = DeviceAuthorizationStatus.Denied,
                };

            await PersistedGrantService.UpdatePayloadAsync(
                openIdContext,
                deviceCodeGrantId,
                updatedPayload,
                cancellationToken
            );

            // The user_code is single-use: revoke the pointer so the code cannot be entered again.
            await PersistedGrantService.SetRevokedOnceAsync(
                openIdContext,
                userCodeGrantId,
                TimeProvider.GetUtcNowWithPrecisionInSeconds(),
                cancellationToken
            );
        }

        return TypedResults.Json(
            new VerificationResult { Status = approved ? ApproveAction : DenyAction },
            openIdEnvironment.JsonSerializerOptions
        );
    }

    private static ChallengeHttpResult ChallengeForLogin(HttpContext httpContext)
    {
        var returnUrl = httpContext.Request.GetEncodedPathAndQuery();
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        return TypedResults.Challenge(properties);
    }

    private sealed class VerificationPrompt
    {
        public required string ClientId { get; init; }
        public required IReadOnlyList<string> Scopes { get; init; }
        public required string UserCode { get; init; }
        public required string Status { get; init; }
    }

    private sealed class VerificationResult
    {
        public required string Status { get; init; }
    }
}
