// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Holds the per-request state that <c>UseBff()</c> sets up and that the session resolver reads.
/// </summary>
internal sealed class BffRequestFeature
{
    /// <summary>
    /// Gets or sets the endpoint that the BFF request checks were run for.
    /// </summary>
    public Endpoint? CheckedEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the session of the request was resolved.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// Gets or sets the resolved session, or <see langword="null"/> when the request has none.
    /// </summary>
    public BffSessionState? Session { get; set; }

    /// <summary>
    /// Gets or sets the principal built from the resolved session.
    /// </summary>
    public ClaimsPrincipal? Principal { get; set; }
}
