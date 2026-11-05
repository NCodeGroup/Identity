// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NCode.Identity.OpenId.Management.Contracts;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Answers a problem that asks the caller to wait. The wait goes in the <c>Retry-After</c> header (RFC 9110 §10.2.3)
/// and in the problem's <c>retryAfterSeconds</c> member, so a client that cannot read the header still knows it.
/// </summary>
// See ADR-1422.
internal static class RetryAfterProblem
{
    /// <summary>
    /// Sets the <c>Retry-After</c> header and the <c>retryAfterSeconds</c> member to the wait, in whole seconds rounded
    /// up and at least one, and returns the problem.
    /// </summary>
    public static ProblemHttpResult Create(
        HttpContext httpContext,
        TimeSpan wait,
        ProblemDetails problem
    )
    {
        var seconds = Math.Max(1L, (long)Math.Ceiling(wait.TotalSeconds));

        httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        problem.Extensions[ManagementProblemMembers.RetryAfterSeconds] = seconds;

        return TypedResults.Problem(problem);
    }
}
