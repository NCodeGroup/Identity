// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointExceptionHandlerMetadata"/> abstraction.
/// </summary>
internal class DefaultOpenIdEndpointExceptionHandlerMetadata(
    Func<
        HttpContext,
        OpenIdEnvironment,
        CancellationToken,
        ValueTask<IOpenIdExceptionHandler>
    > getter
) : IOpenIdEndpointExceptionHandlerMetadata
{
    private Func<
        HttpContext,
        OpenIdEnvironment,
        CancellationToken,
        ValueTask<IOpenIdExceptionHandler>
    > Getter { get; } = getter;

    /// <inheritdoc />
    public async ValueTask<IOpenIdExceptionHandler> GetExceptionHandlerAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    ) => await Getter(httpContext, openIdEnvironment, cancellationToken);
}
