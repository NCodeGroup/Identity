// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint convention for the management <c>/api</c> surface that answers every error response that carries no
/// body as a problem document. It also answers a request whose binding failed with the status that binding chose.
/// </summary>
// See ADR-1483.
internal static class ProblemResponseEndpointConvention
{
    /// <summary>
    /// Wraps the request delegate of an endpoint so that a response that ends with an error status and no body is
    /// answered as a problem document.
    /// </summary>
    /// <param name="builder">The endpoint to wrap.</param>
    public static void Apply(EndpointBuilder builder)
    {
        var next =
            builder.RequestDelegate
            ?? throw new InvalidOperationException("The endpoint has no request delegate to wrap.");

        builder.RequestDelegate = context => InvokeAsync(next, context);
    }

    internal static async Task InvokeAsync(RequestDelegate next, HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (BadHttpRequestException exception) when (!httpContext.Response.HasStarted)
        {
            httpContext.Response.StatusCode = exception.StatusCode;
        }

        var response = httpContext.Response;
        if (
            !response.HasStarted
            && response.StatusCode is >= StatusCodes.Status400BadRequest and < 600
            && response.ContentLength is null
            && string.IsNullOrEmpty(response.ContentType)
        )
        {
            await TypedResults.Problem(statusCode: response.StatusCode).ExecuteAsync(httpContext);
        }
    }
}
