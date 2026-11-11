// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.AspNetCore;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides an implementation of the <see cref="IResult"/> abstraction that redirects an HTTP client to a specified
/// URL and is <c>AJAX</c> aware.
/// </summary>
[PublicAPI]
public sealed class OpenIdRedirectResult : IResult
{
    /// <summary>
    /// Gets the URL used for the redirect operation.
    /// </summary>
    public required string RedirectUrl { get; init; }

    /// <summary>
    /// Gets the function that is used to determine if the current request is an <c>AJAX</c> request.
    /// If <c>null</c>, a default implementation is provided that checks the <c>X-Requested-With</c> header for the
    /// value <c>XMLHttpRequest</c>.
    /// See the following for more information: https://github.com/aspnet/Security/issues/1394
    /// </summary>
    public Func<HttpContext, bool>? IsAjaxRequest { get; init; }

    /// <summary>
    /// Gets the HTTP status code that is only used for <c>AJAX</c> redirects.
    /// Browser redirects will always use status code <c>302</c>.
    /// </summary>
    public int? AjaxStatusCode { get; init; }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var executor = httpContext.RequestServices.GetRequiredService<
            IResultExecutor<OpenIdRedirectResult>
        >();
        await executor.ExecuteAsync(httpContext, this, httpContext.RequestAborted);
    }
}
