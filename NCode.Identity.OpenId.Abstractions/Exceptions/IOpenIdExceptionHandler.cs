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

using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Provides a way to handle unexpected errors that occur during an <c>OAuth</c> or <c>OpenID Connect</c> endpoint.
/// </summary>
[PublicAPI]
public interface IOpenIdExceptionHandler
{
    /// <summary>
    /// Handles an unexpected error that occurred during an <c>OAuth</c> or <c>OpenID Connect</c> endpoint.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="mediator">The <see cref="IMediator"/> instance that may be used to publish events or notifications.</param>
    /// <param name="exceptionDispatchInfo">The exception that occurred.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="EndpointDisposition"/> for the <c>OAuth</c> or <c>OpenID Connect</c> operation.</returns>
    ValueTask<EndpointDisposition> HandleExceptionAsync(
        OpenIdEnvironment openIdEnvironment,
        HttpContext httpContext,
        IMediator mediator,
        ExceptionDispatchInfo exceptionDispatchInfo,
        CancellationToken cancellationToken
    );
}
