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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// An implementation of <see cref="IResult"/> that when executed, issues the response for an
/// <c>OAuth</c> or <c>OpenID Connect</c> operation that may have succeeded or failed.
/// </summary>
[PublicAPI]
public class OpenIdResult<T> : IResult, ISupportError
    where T : class, IOpenIdMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdResult{T}"/> class for a failed operation.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> that contains information about the failure of the operation.</param>
    public OpenIdResult(IOpenIdError error)
        : this(error, null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdResult{T}"/> class for a successful operation.
    /// </summary>
    /// <param name="response">The response that contains the parameters for a successful <c>OAuth</c> or <c>OpenID Connect</c>
    /// operation.
    /// </param>
    public OpenIdResult(T response)
        : this(null, response)
    {
        // nothing
    }

    private OpenIdResult(IOpenIdError? error, T? response)
    {
        Error = error;
        Response = response;
    }

    /// <summary>
    /// Gets a value indicating whether the authorization operation was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    [MemberNotNullWhen(true, nameof(Response))]
    public bool Succeeded => Response != null;

    /// <summary>
    /// Gets the <see cref="IOpenIdError"/> that contains information about the failure of the operation.
    /// </summary>
    public IOpenIdError? Error { get; }

    /// <summary>
    /// Gets the response that contains the parameters for a successful <c>OAuth</c> or <c>OpenID Connect</c> operation.
    /// </summary>
    public T? Response { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        IOpenIdMessage message = Succeeded ? Response : Error;
        var jsonSerializerOptions = message.OpenIdServer.JsonSerializerOptions;
        var statusCode = message is ISupportStatusCode supportStatusCode ? supportStatusCode.StatusCode : null;
        var result = TypedResults.Json(
            message,
            jsonSerializerOptions,
            statusCode: statusCode);
        await result.ExecuteAsync(httpContext);
    }
}
