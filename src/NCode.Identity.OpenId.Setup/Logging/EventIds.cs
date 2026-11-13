// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Setup.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.OpenId.Setup</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>29000</c>–<c>29999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier. Each constant is named exactly as the <c>Log</c> method it identifies.
/// </remarks>
internal static class EventIds
{
    private const int Base = 29000;

    public const int SetupTokenIssued = Base + 1;
    public const int SetupTokenIssuedByAnotherReplica = Base + 2;
    public const int RootAdministratorCreated = Base + 3;
    public const int InstanceSetupClientRegistered = Base + 4;
}
