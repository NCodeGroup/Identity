// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.AspNetCore.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.AspNetCore</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>28000</c>–<c>28999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 28000;

    public const int ProxyClientCertificateRefused = Base + 1;

    public const int ProxyClientCertificateMissing = Base + 2;
}
