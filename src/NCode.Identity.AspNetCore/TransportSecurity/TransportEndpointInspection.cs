// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.AspNetCore.TransportSecurity;

/// <summary>
/// The outcome of inspecting the endpoints a server bound.
/// </summary>
/// <param name="Failures">A sentence for each endpoint rule that is broken, or an empty list when every endpoint is HTTPS.</param>
/// <param name="TlsEndpoints">The HTTPS endpoints on a TCP port, whose certificates are still to be probed.</param>
internal sealed record TransportEndpointInspection(
    IReadOnlyList<string> Failures,
    IReadOnlyList<Uri> TlsEndpoints
);
