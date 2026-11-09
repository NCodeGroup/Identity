// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// A middleware that answers every response under the <c>/api</c> prefix that ends with an error status and no body as
/// a problem document: a management route's answer, a failed request binding's answer, the router's <c>404</c>,
/// <c>405</c> and <c>415</c>, and the answer of any endpoint a host maps under the prefix.
/// </summary>
// See ADR-1483.
internal class ManagementProblemResponseMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Runs the rest of the pipeline, then answers a response under the <c>/api</c> prefix as a problem document when
    /// the response has not started, its status is from <c>400</c> to <c>599</c>, and it has neither a
    /// <c>Content-Length</c> nor a <c>Content-Type</c>.
    /// </summary>
    /// <param name="httpContext">The context of the request.</param>
    /// <returns>A task that completes when the response is answered.</returns>
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
            && ErrorStatusCodes.Contains(response.StatusCode)
            && response.ContentLength is null
            && string.IsNullOrEmpty(response.ContentType)
        )
        {
            await TypedResults.Problem(statusCode: response.StatusCode).ExecuteAsync(httpContext);
        }
    }
}
