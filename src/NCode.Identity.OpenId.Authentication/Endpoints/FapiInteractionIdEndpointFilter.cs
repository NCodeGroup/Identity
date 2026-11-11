// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides an <see cref="IEndpointFilter"/> that answers every request with the <c>x-fapi-interaction-id</c> header
/// (FAPI 1.0 Part 1 §6.2.1-11): the value the client sent when it is a UUID (RFC 4122), and a new UUID otherwise. The
/// header is set before the endpoint runs, so success and error results alike carry it.
/// </summary>
internal sealed class FapiInteractionIdEndpointFilter : IEndpointFilter
{
    /// <summary>
    /// The name of the request and response header that carries the interaction identifier.
    /// </summary>
    public const string HeaderName = "x-fapi-interaction-id";

    private const string UuidFormat = "D";

    /// <summary>
    /// Gets the singleton instance of <see cref="FapiInteractionIdEndpointFilter"/>.
    /// </summary>
    public static FapiInteractionIdEndpointFilter Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        var requested = httpContext.Request.Headers[HeaderName].ToString();

        var interactionId = Guid.TryParseExact(requested, UuidFormat, out _)
            ? requested
            : Guid.NewGuid().ToString(UuidFormat);

        httpContext.Response.Headers[HeaderName] = interactionId;

        return next(context);
    }
}
