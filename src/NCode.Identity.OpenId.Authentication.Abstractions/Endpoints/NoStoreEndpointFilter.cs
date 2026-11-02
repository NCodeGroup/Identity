// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides an <see cref="IEndpointFilter"/> that marks the response of an endpoint as non-cacheable before the
/// endpoint runs, so that success and error results alike carry the headers.
/// </summary>
internal sealed class NoStoreEndpointFilter : IEndpointFilter
{
    /// <summary>
    /// Gets the singleton instance of <see cref="NoStoreEndpointFilter"/>.
    /// </summary>
    public static NoStoreEndpointFilter Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        context.HttpContext.Response.SetNoStore();
        return next(context);
    }
}
