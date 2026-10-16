#region Copyright Preamble

// Copyright @ 2024 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Results;

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
