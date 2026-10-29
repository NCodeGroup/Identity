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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Antiforgery;

/// <summary>
/// Applies the BFF request checks before authentication, and answers the BFF's store and token failures.
/// </summary>
internal class BffMiddleware(
    RequestDelegate next,
    IOptions<BffOptions> options,
    IBffAuditRecorder recorder
)
{
    private readonly RequestDelegate _next = next;
    private readonly IOptions<BffOptions> _options = options;
    private readonly IBffAuditRecorder _recorder = recorder;

    /// <summary>
    /// Runs the middleware for a request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Features.Get<IAuthenticationFeature>() is not null)
        {
            throw new InvalidOperationException(
                "UseBff() must be called before UseAuthentication()."
            );
        }

        var feature = new BffRequestFeature();
        context.Features.Set(feature);

        var endpoint = context.GetEndpoint();
        if (BffGuardedEndpoints.IsGuarded(endpoint))
            feature.CheckedEndpoint = endpoint;

        if (BffGuardedEndpoints.RequiresAntiforgery(endpoint))
        {
            var reason = BffRequestGuard.Check(context.Request, _options.Value);
            if (reason is not null)
            {
                await _recorder.AntiforgeryRejectedAsync(context, reason, context.RequestAborted);

                var (type, title) = reason switch
                {
                    BffRequestGuard.AntiforgeryHeaderMissing => (
                        BffProblemTypes.AntiforgeryHeaderMissing,
                        "The anti-forgery header is missing or invalid."
                    ),
                    _ => (BffProblemTypes.CrossSiteRequest, "Cross-site requests are not allowed."),
                };

                await BffResponses.WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    type,
                    title
                );
                return;
            }
        }

        try
        {
            await _next(context);
        }
        catch (BffStoreUnavailableException) when (!context.Response.HasStarted)
        {
            await BffResponses.WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                BffProblemTypes.StoreUnavailable,
                "The BFF session store is unavailable."
            );
        }
        catch (BffTokenUnavailableException exception) when (!context.Response.HasStarted)
        {
            var type =
                exception.StatusCode == StatusCodes.Status401Unauthorized
                    ? BffProblemTypes.SessionRequired
                    : BffProblemTypes.TokenUnavailable;

            await BffResponses.WriteProblemAsync(
                context,
                exception.StatusCode,
                type,
                "An access token is unavailable."
            );
        }
    }
}
