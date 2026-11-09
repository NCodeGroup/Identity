// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// A middleware that answers every management response that ends with an error status and no body as a problem
/// document: a route's answer, a failed request binding's answer, and the router's <c>404</c>, <c>405</c> and
/// <c>415</c>.
/// </summary>
// See ADR-1483.
internal sealed class ManagementProblemResponseMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var isManagementRequest = httpContext.Request.Path.StartsWithSegments(
            ManagementRootGroup.RoutePrefix
        );

        await next(httpContext);

        if (!isManagementRequest)
            return;

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
