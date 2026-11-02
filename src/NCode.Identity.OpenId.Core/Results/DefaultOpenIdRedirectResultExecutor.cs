// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NCode.Identity.Results;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides the default implementation of <see cref="IResultExecutor{T}"/> for <see cref="OpenIdRedirectResult"/>.
/// </summary>
internal class DefaultOpenIdRedirectResultExecutor : IResultExecutor<OpenIdRedirectResult>
{
    private const string AjaxHeaderValue = "XMLHttpRequest";

    private static bool IsAjaxRequest(HttpContext httpContext) =>
        string.Equals(
            httpContext.Request.Query[HeaderNames.XRequestedWith],
            AjaxHeaderValue,
            StringComparison.Ordinal
        )
        || string.Equals(
            httpContext.Request.Headers.XRequestedWith,
            AjaxHeaderValue,
            StringComparison.Ordinal
        );

    /// <inheritdoc />
    public ValueTask ExecuteAsync(
        HttpContext httpContext,
        OpenIdRedirectResult result,
        CancellationToken cancellationToken
    )
    {
        var isAjaxRequest = result.IsAjaxRequest ?? IsAjaxRequest;
        if (isAjaxRequest(httpContext))
        {
            httpContext.Response.Headers.Location = result.RedirectUrl;

            if (result.AjaxStatusCode.HasValue)
                httpContext.Response.StatusCode = result.AjaxStatusCode.Value;
        }
        else
        {
            httpContext.Response.Redirect(result.RedirectUrl);
        }

        return ValueTask.CompletedTask;
    }
}
