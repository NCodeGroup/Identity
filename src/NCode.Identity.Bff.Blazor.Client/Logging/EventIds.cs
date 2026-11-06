// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Blazor.Client.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.Bff.Blazor.Client</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>24000</c>–<c>24999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 24000;

    public const int ProbeFailed = Base + 1;

    public const int ProbeStatusUnexpected = Base + 2;

    public const int ProbeDocumentUnreadable = Base + 3;
}
