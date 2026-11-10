// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint convention for the management <c>/api</c> surface. When minimal APIs throw a request-binding failure
/// (<c>RouteHandlerOptions.ThrowOnBadRequest</c>, on in Development), it answers the failure with its status before a
/// host's exception handler renders it, and the management problem middleware writes the problem document.
/// </summary>
// See ADR-1483.
internal static class BindingExceptionEndpointConvention
{
    /// <summary>
    /// Wraps the request delegate of an endpoint so that a thrown request-binding failure is answered with its status.
    /// </summary>
    /// <param name="builder">The endpoint to wrap.</param>
    public static void Apply(EndpointBuilder builder)
    {
        var next =
            builder.RequestDelegate
            ?? throw new InvalidOperationException("The endpoint has no request delegate to wrap.");

        builder.RequestDelegate = context =>
            InvokeAsync(next, context, context.RequestAborted).AsTask();
    }

    /// <summary>
    /// Runs the wrapped request delegate and answers a request-binding failure it throws with the failure's status and
    /// no body, unless the response has started.
    /// </summary>
    /// <param name="next">The request delegate of the endpoint.</param>
    /// <param name="httpContext">The context of the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the request is answered.</returns>
    private static async ValueTask InvokeAsync(
        RequestDelegate next,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await next(httpContext);
        }
        catch (BadHttpRequestException exception) when (!httpContext.Response.HasStarted)
        {
            httpContext.Response.StatusCode = exception.StatusCode;
        }
    }
}
