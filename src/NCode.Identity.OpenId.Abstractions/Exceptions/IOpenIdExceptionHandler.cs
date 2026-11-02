// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

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
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ReadOnlyEndpointDisposition"/> for the <c>OAuth</c> or <c>OpenID Connect</c> operation.</returns>
    ValueTask<ReadOnlyEndpointDisposition> HandleExceptionAsync(
        OpenIdEnvironment openIdEnvironment,
        HttpContext httpContext,
        IMediator mediator,
        ExceptionDispatchInfo exceptionDispatchInfo,
        CancellationToken cancellationToken
    );
}
