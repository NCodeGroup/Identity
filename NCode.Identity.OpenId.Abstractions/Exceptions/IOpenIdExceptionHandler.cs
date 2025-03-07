#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Provides a way to handle exceptions that occur during <c>OAuth</c> or <c>OpenID Connect</c> operations.
/// </summary>
[PublicAPI]
public interface IOpenIdExceptionHandler
{
    /// <summary>
    /// Handles an exception that occurred during an <c>OAuth</c> or <c>OpenID Connect</c> operation.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the HTTP response to send to the client.</returns>
    ValueTask<IResult> HandleExceptionAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        Exception exception,
        CancellationToken cancellationToken
    );
}
