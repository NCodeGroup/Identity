// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Requests;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Logout;

/// <summary>
/// Handles <c>POST /backchannel-logout</c>: validates a logout token and ends the sessions it names.
/// </summary>
internal class BffBackChannelLogoutHandler(
    BffClientRegistrationProvider registrationProvider,
    ILogoutTokenValidator logoutTokenValidator,
    BffInstrumentedSessionStore sessionStore,
    IBffSessionManager sessionManager,
    IBffAuditRecorder auditRecorder
)
{
    /// <summary>
    /// The audit reason when a session could not be ended.
    /// </summary>
    internal const string SessionEndFailedReason = "session_end_failed";

    /// <summary>
    /// The audit reason when the provider's keys or metadata could not be read to validate the token.
    /// </summary>
    internal const string ProviderUnavailableReason = BffLoginFailureReasons.ProviderUnavailable;

    /// <summary>
    /// Handles the back-channel logout request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public virtual async Task HandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;
        var cancellationToken = httpContext.RequestAborted;

        BffResponses.SetNoStore(httpContext.Response);

        if (!request.HasFormContentType)
        {
            await RejectAsync(httpContext, OpenIdProtocol.ErrorCodes.InvalidRequest);
            return;
        }

        var form = await BffForms.TryReadFormAsync(request, cancellationToken);
        if (form is null)
        {
            await RejectAsync(httpContext, OpenIdProtocol.ErrorCodes.InvalidRequest);
            return;
        }

        var tokenValues = form[OpenIdProtocol.Parameters.LogoutToken];
        var logoutToken = tokenValues.Count == 1 ? tokenValues[0] : null;
        if (string.IsNullOrEmpty(logoutToken))
        {
            await RejectAsync(httpContext, OpenIdProtocol.ErrorCodes.InvalidRequest);
            return;
        }

        var registration = registrationProvider.Registration;
        var validationRequest = new LogoutTokenValidationRequest { LogoutToken = logoutToken };
        LogoutTokenValidationResult result;
        try
        {
            result = await logoutTokenValidator.ValidateAsync(
                registration,
                validationRequest,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            await RejectAsync(httpContext, ProviderUnavailableReason);
            return;
        }

        if (!result.IsValid)
        {
            await RejectAsync(httpContext, result.FailureCode);
            return;
        }

        try
        {
            var keys = await FindAsync(result, cancellationToken);
            foreach (var key in keys)
            {
                var record = await sessionStore.GetAsync(key, cancellationToken);
                if (record is null)
                    continue;

                var metadata = record.Metadata;
                if (!string.Equals(metadata.Issuer, result.Issuer, StringComparison.Ordinal))
                    continue;

                if (
                    result.SessionId is not null
                    && !string.Equals(
                        metadata.SessionId,
                        result.SessionId,
                        StringComparison.Ordinal
                    )
                )
                    continue;

                await sessionManager.EndAsync(
                    httpContext,
                    key,
                    metadata,
                    BffSessionEndReasons.BackchannelLogout,
                    CancellationToken.None
                );
            }
        }
        catch (BffStoreUnavailableException)
        {
            await RejectAsync(httpContext, SessionEndFailedReason);
            return;
        }

        // ADR-1284: the token is recorded only after the sessions it names have ended.
        await logoutTokenValidator.TryRecordHandledAsync(
            registration,
            result,
            CancellationToken.None
        );
        await auditRecorder.BackchannelLogoutReceivedAsync(httpContext, cancellationToken);

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
    }

    private async ValueTask<IReadOnlyList<string>> FindAsync(
        LogoutTokenValidationResult result,
        CancellationToken cancellationToken
    )
    {
        var issuer = result.Issuer ?? string.Empty;
        if (!string.IsNullOrEmpty(result.SessionId))
            return await sessionStore.FindBySessionIdAsync(
                issuer,
                result.SessionId,
                cancellationToken
            );

        return await sessionStore.FindBySubjectAsync(
            issuer,
            result.Subject ?? string.Empty,
            cancellationToken
        );
    }

    private async Task RejectAsync(HttpContext httpContext, string reason)
    {
        await auditRecorder.BackchannelLogoutRejectedAsync(
            httpContext,
            reason,
            httpContext.RequestAborted
        );
        await BffResponses.WriteProblemAsync(
            httpContext,
            StatusCodes.Status400BadRequest,
            BffProblemTypes.InvalidRequest,
            "The logout token was rejected."
        );
    }
}
