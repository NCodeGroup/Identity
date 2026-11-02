// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Metrics;
using NCode.Identity.OpenId.Results;
using NCode.Identity.Results;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Executes an <see cref="AuthorizationResult"/>: an error with an explicit HTTP status code is answered directly, and
/// every other response is delivered through the <see cref="IAuthorizationResponseModeHandler"/> of the result's
/// <see cref="AuthorizationResult.Delivery"/> (ADR-1201).
/// </summary>
internal class DefaultAuthorizationResultExecutor : IResultExecutor<AuthorizationResult>
{
    public async ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        CancellationToken cancellationToken
    )
    {
        // If an error occurred with an explicit HTTP status code,
        // then return a JSON response with the corresponding status code
        // instead of returning a redirect response.
        if (result.Error is { StatusCode: not null })
        {
            var httpResult = result.Error.AsHttpResult();
            await httpResult.ExecuteAsync(httpContext);
            return;
        }

        // An error returned to the client through its redirect URI never reaches the JSON result executor, so it is
        // tagged here with the same server-chosen error code (ADR-1197).
        if (result.Error is not null)
            OpenIdMetricAttributes.Add(
                httpContext,
                OpenIdMetricAttributes.Error,
                result.Error.Code
            );

        IOpenIdMessageResponse? error = result.Error;
        IOpenIdMessageResponse? ticket = result.Ticket;

        var message =
            error
            ?? ticket
            ?? throw new InvalidOperationException("Both error and ticket are null.");

        await result.Delivery.ResponseModeHandler.ExecuteAsync(
            httpContext,
            result,
            message,
            cancellationToken
        );
    }
}
