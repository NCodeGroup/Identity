// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Represents the BFF session that was resolved for the current request.
/// </summary>
/// <remarks>
/// The feature is set on <c>HttpContext.Features</c> when the request carries a valid session cookie.
/// </remarks>
[PublicAPI]
public interface IBffSessionFeature
{
    /// <summary>
    /// Gets the metadata of the resolved session.
    /// </summary>
    BffSessionMetadata Metadata { get; }

    /// <summary>
    /// Gets the principal built from the claims stored in the resolved session.
    /// </summary>
    ClaimsPrincipal Principal { get; }
}
