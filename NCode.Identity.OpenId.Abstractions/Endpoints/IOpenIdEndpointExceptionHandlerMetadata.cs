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
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Endpoints;

/// <summary>
/// Represents metadata for an <c>OAuth</c> or <c>OpenID Connect</c> endpoint that provides its own exception handling.
/// </summary>
[PublicAPI]
public interface IOpenIdEndpointExceptionHandlerMetadata
{
    /// <summary>
    /// Gets the <see cref="IOpenIdExceptionHandler"/> for the associated endpoint.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="IOpenIdExceptionHandler"/> instance.</returns>
    ValueTask<IOpenIdExceptionHandler> GetExceptionHandlerAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken);
}
