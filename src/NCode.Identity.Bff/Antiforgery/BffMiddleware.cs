// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
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
    BffSessionResolver resolver,
    IAntiforgery antiforgery,
    IBffAuditRecorder recorder
)
{
    private RequestDelegate Next { get; } = next;
    private IOptions<BffOptions> Options { get; } = options;
    private BffFrontendCatalog Frontends { get; } = frontends;
    private BffSessionResolver Resolver { get; } = resolver;
    private IAntiforgery Antiforgery { get; } = antiforgery;
    private IBffAuditRecorder Recorder { get; } = recorder;

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

        try
        {
            var refusal = await GetRefusalAsync(context, endpoint, feature, context.RequestAborted);
            if (refusal is { } refused)
            {
                await Recorder.AntiforgeryRejectedAsync(
                    context,
                    refused.Reason,
                    context.RequestAborted
                );

                await BffResponses.WriteProblemAsync(
                    context,
                    refused.StatusCode,
                    refused.Type,
                    refused.Title
                );
                return;
            }

            await Next(context);
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

    private async ValueTask<(
        string Reason,
        int StatusCode,
        string Type,
        string Title
    )?> GetRefusalAsync(
        HttpContext context,
        Endpoint? endpoint,
        BffRequestFeature feature,
        CancellationToken cancellationToken
    )
    {
        if (BffGuardedEndpoints.RequiresAntiforgery(endpoint))
        {
            var trustedOrigins = Frontends.Resolve(context)?.Options.TrustedOrigins ?? [];
            var reason = BffRequestGuard.Check(
                context.Request,
                Options.Value.Antiforgery,
                trustedOrigins
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

        if (BffGuardedEndpoints.IsHub(endpoint))
        {
            var frontend = Frontends.Resolve(context);
            var reason = BffRequestGuard.CheckHub(
                context.Request,
                frontend?.Options.PublicOrigin,
                frontend?.Options.TrustedOrigins ?? []
            );
            if (reason is null)
                return null;

            return (
                reason,
                StatusCodes.Status403Forbidden,
                BffProblemTypes.CrossSiteRequest,
                "Cross-site requests are not allowed."
            );
        }

        if (BffGuardedEndpoints.IsPage(endpoint))
        {
            if (BffRequestGuard.IsSafeMethod(context.Request.Method))
                return null;

            var frontend = Frontends.Resolve(context);
            var reason = BffRequestGuard.CheckFetchMetadata(
                context.Request,
                frontend?.Options.TrustedOrigins ?? []
            );
            if (reason is not null)
            {
                return (
                    reason,
                    StatusCodes.Status403Forbidden,
                    BffProblemTypes.CrossSiteRequest,
                    "Cross-site requests are not allowed."
                );
            }

            // Resolving the session sets the request feature's principal, which the antiforgery token is bound to.
            var session = await Resolver.ResolveAsync(context, cancellationToken);
            var principal = session is null ? null : feature.Principal;

            if (await IsAntiforgeryTokenValidAsync(context, principal, cancellationToken))
                return null;

            return (
                BffRequestGuard.AntiforgeryTokenInvalid,
                StatusCodes.Status403Forbidden,
                BffProblemTypes.AntiforgeryTokenInvalid,
                "The antiforgery token is missing or invalid."
            );
        }

        return null;
    }

    private async ValueTask<bool> IsAntiforgeryTokenValidAsync(
        HttpContext context,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken
    )
    {
        var original = context.User;
        context.User = principal ?? new ClaimsPrincipal();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Antiforgery.IsRequestValidAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
        finally
        {
            context.User = original;
        }
    }
}
