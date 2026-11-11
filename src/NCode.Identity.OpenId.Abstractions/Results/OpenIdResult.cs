// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides an implementation of <see cref="IResult"/> that when executed, writes an <see cref="IOpenIdResponse"/> to
/// the HTTP response. Execution is delegated to the registered <see cref="IResultExecutor{T}"/>, which content-
/// negotiates the representation (the default JSON body, or an alternate such as an HTML error page when an
/// interactive client requests it).
/// </summary>
/// <param name="response">The <see cref="IOpenIdResponse"/> that contains information about the <c>OAuth</c> or <c>OpenID Connect</c> operation.</param>
[PublicAPI]
public sealed class OpenIdResult<T>(T response) : IResult, IOpenIdResult
    where T : class, IOpenIdResponse
{
    /// <summary>
    /// Gets the response that contains the information about the <c>OAuth</c> or <c>OpenID Connect</c> operation.
    /// </summary>
    public T Response { get; } = response;

    /// <inheritdoc />
    IOpenIdResponse IOpenIdResult.Response => Response;

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var executor = httpContext.RequestServices.GetRequiredService<
            IResultExecutor<IOpenIdResult>
        >();
        await executor.ExecuteAsync(httpContext, this, httpContext.RequestAborted);
    }
}
