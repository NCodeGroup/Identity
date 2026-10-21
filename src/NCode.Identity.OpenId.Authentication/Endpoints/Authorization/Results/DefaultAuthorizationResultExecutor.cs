#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Metrics;
using NCode.Identity.OpenId.Results;
using NCode.Identity.Results;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Executes an <see cref="AuthorizationResult"/>: an error with an explicit HTTP status code is answered directly, and
/// every other response is delivered by the <see cref="IAuthorizationResponseModeHandler"/> registered for the result's
/// response mode (ADR-0201).
/// </summary>
internal class DefaultAuthorizationResultExecutor(
    IEnumerable<IAuthorizationResponseModeHandler> responseModeHandlers
) : IResultExecutor<AuthorizationResult>
{
    private IReadOnlyDictionary<
        string,
        IAuthorizationResponseModeHandler
    > ResponseModeHandlers { get; } =
        responseModeHandlers
            .GroupBy(handler => handler.ResponseMode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

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
        // tagged here with the same server-chosen error code (ADR-0197).
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

        if (!ResponseModeHandlers.TryGetValue(result.ResponseMode, out var handler))
            throw new InvalidOperationException(
                $"No handler is registered for the '{result.ResponseMode}' response mode."
            );

        await handler.ExecuteAsync(httpContext, result, message, cancellationToken);
    }
}
