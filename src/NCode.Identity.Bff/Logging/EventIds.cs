// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.Bff</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>19000</c>–<c>19999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 19000;

    public const int LoginFailed = Base + 1;
    public const int RefreshFailed = Base + 2;
    public const int RefreshTokenRevocationFailed = Base + 3;
    public const int RefreshLeaseReleaseFailed = Base + 4;
    public const int TokenRequestFailed = Base + 5;
    public const int RemoteApiForwardFailed = Base + 6;
    public const int EndpointCheckFailed = Base + 7;
    public const int ConnectionRevalidationFailed = Base + 8;
    public const int ConnectionRevalidationFaulted = Base + 9;
    public const int HubRevalidationFaulted = Base + 10;
}
