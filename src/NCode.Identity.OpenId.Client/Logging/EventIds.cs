// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.OpenId.Client</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>18000</c>–<c>18999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 18000;

    public const int ProviderMetadataFetchFailed = Base + 1;
    public const int ProviderKeySetFetchFailed = Base + 2;
}
