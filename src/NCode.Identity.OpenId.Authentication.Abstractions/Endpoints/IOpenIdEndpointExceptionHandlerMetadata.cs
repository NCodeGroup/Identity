// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents metadata for an <c>OAuth</c> or <c>OpenID Connect</c> endpoint that provides its own exception handling.
/// </summary>
[PublicAPI]
public interface IOpenIdEndpointExceptionHandlerMetadata
{
    /// <summary>
    /// Gets the <see cref="IOpenIdExceptionHandler"/> instance for the associated endpoint.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="IOpenIdExceptionHandler"/> instance.</returns>
    ValueTask<IOpenIdExceptionHandler> GetExceptionHandlerAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    );
}
