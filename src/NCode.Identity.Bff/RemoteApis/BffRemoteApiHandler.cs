// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Responses;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Forwards a request to a remote API: it checks the method and the path, obtains the API's token, and streams the
/// request and response through YARP's <see cref="IHttpForwarder"/>.
/// </summary>
internal sealed class BffRemoteApiHandler(
    IHttpForwarder forwarder,
    IBffTokenAccessor tokens,
    BffFrontendCatalog frontends,
    ILogger<BffRemoteApiHandler> logger
)
{
    /// <summary>
    /// Forwards the current request to the specified remote API.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="api">The remote API that the request was routed to.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public async Task HandleAsync(HttpContext context, BffRemoteApi api)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(api);

        if (!api.AllowsMethod(context.Request.Method))
        {
            context.Response.Headers.Allow = api.AllowHeader;
            await BffResponses.WriteProblemAsync(
                context,
                StatusCodes.Status405MethodNotAllowed,
                BffProblemTypes.MethodNotAllowed,
                "The remote API does not accept this method."
            );
            return;
        }

        if (!BffRemoteApiPaths.TryGetRemainder(context, out var remainder))
        {
            await BffResponses.WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                BffProblemTypes.InvalidRequest,
                "The request path cannot be forwarded."
            );
            return;
        }

        var token = await tokens.GetAccessTokenAsync(
            context,
            api.TokenRequest,
            context.RequestAborted
        );
        if (!token.IsSuccess)
            throw new BffTokenUnavailableException(token.StatusCode, token.Reason);

        var transformer = new BffRemoteApiTransformer(
            remainder,
            token,
            frontends.Current.Options.Antiforgery.HeaderName
        );

        var error = await forwarder.SendAsync(
            context,
            api.Destination,
            api.Invoker,
            api.RequestConfig,
            transformer
        );

        if (error != ForwarderError.None && !context.RequestAborted.IsCancellationRequested)
        {
            var exception = context.GetForwarderErrorFeature()?.Exception;
            logger.RemoteApiForwardFailed(api.Path, error.ToString(), exception);
        }
    }
}
