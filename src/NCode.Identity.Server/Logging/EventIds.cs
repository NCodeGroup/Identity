// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Server.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.Server</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>12000</c>–<c>12999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier. Each constant is named exactly as the <c>Log</c> method it identifies.
/// </remarks>
internal static class EventIds
{
    private const int Base = 12000;

    public const int BootstrapAdminClientSeeded = Base + 1;
    public const int BootstrapAdminClientSecretRefreshed = Base + 2;
    public const int ConformanceClientSeeded = Base + 3;
    public const int ConformanceTestUserSeeded = Base + 4;
    public const int LocalRootKeyInProduction = Base + 5;
    public const int KeyRingKeyUndecryptable = Base + 6;
    public const int KeyRingKeyRevoked = Base + 7;
    public const int KeyRingMasterKeyNotLoaded = Base + 8;
    public const int ServerSecretProtectorRefreshFailed = Base + 9;
}
