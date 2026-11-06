// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
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
    BffFrontendCatalog frontends,
    IBffAuditRecorder recorder
)
{
    private const string PageAllowedMethods = "GET, HEAD";

    private readonly RequestDelegate _next = next;
    private readonly IOptions<BffOptions> _options = options;
    private readonly BffFrontendCatalog _frontends = frontends;
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

        var refusal = GetRefusal(context, endpoint);
        if (refusal is { } refused)
        {
            await _recorder.AntiforgeryRejectedAsync(
                context,
                refused.Reason,
                context.RequestAborted
            );

            if (refused.StatusCode == StatusCodes.Status405MethodNotAllowed)
                context.Response.Headers.Allow = PageAllowedMethods;

            await BffResponses.WriteProblemAsync(
                context,
                refused.StatusCode,
                refused.Type,
                refused.Title
            );
            return;
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
            await BffResponses.WriteTokenUnavailableAsync(context, exception.StatusCode);
        }
    }

    private (string Reason, int StatusCode, string Type, string Title)? GetRefusal(
        HttpContext context,
        Endpoint? endpoint
    )
    {
        if (BffGuardedEndpoints.RequiresAntiforgery(endpoint))
        {
            var reason = BffRequestGuard.Check(
                context.Request,
                _options.Value.Antiforgery,
                _frontends.Resolve(context)?.Options.TrustedOrigins ?? []
            );
            if (reason is null)
                return null;

            var (type, title) = reason switch
            {
                BffRequestGuard.AntiforgeryHeaderMissing => (
                    BffProblemTypes.AntiforgeryHeaderMissing,
                    "The anti-forgery header is missing or invalid."
                ),
                _ => (BffProblemTypes.CrossSiteRequest, "Cross-site requests are not allowed."),
            };

            return (reason, StatusCodes.Status403Forbidden, type, title);
        }

        if (BffGuardedEndpoints.IsPage(endpoint))
        {
            var reason = BffRequestGuard.CheckPage(context.Request);
            if (reason is null)
                return null;

            return (
                reason,
                StatusCodes.Status405MethodNotAllowed,
                BffProblemTypes.UnsafeMethod,
                "A page accepts only GET and HEAD."
            );
        }

        return null;
    }
}
