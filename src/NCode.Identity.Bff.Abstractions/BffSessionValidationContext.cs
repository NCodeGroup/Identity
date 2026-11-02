// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// Carries what an <see cref="IBffSessionValidator"/> needs to decide whether a session may continue.
/// </summary>
/// <param name="httpContext">The HTTP context of the request that resolved the session.</param>
/// <param name="metadata">The metadata of the resolved session.</param>
/// <param name="principal">The principal built from the claims stored in the session.</param>
[PublicAPI]
public sealed class BffSessionValidationContext(
    HttpContext httpContext,
    BffSessionMetadata metadata,
    ClaimsPrincipal principal
)
{
    /// <summary>
    /// Gets the HTTP context of the request that resolved the session.
    /// </summary>
    public HttpContext HttpContext { get; } =
        httpContext ?? throw new ArgumentNullException(nameof(httpContext));

    /// <summary>
    /// Gets the metadata of the resolved session.
    /// </summary>
    public BffSessionMetadata Metadata { get; } =
        metadata ?? throw new ArgumentNullException(nameof(metadata));

    /// <summary>
    /// Gets the principal built from the claims stored in the session.
    /// </summary>
    public ClaimsPrincipal Principal { get; } =
        principal ?? throw new ArgumentNullException(nameof(principal));
}
