#region Copyright Preamble

// Copyright @ 2025 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

namespace NCode.Identity.OpenId.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.OpenId.Core</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>1000</c>–<c>1999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 1000;

    public const int UnhandledException = Base + 1;

    public const int ClientError = Base + 2;

    public const int ServerSettingsReloadRejected = Base + 3;

    public const int TenantKeyProvisioned = Base + 4;

    public const int TenantKeyReseedFailed = Base + 5;

    public const int TenantChangeBroadcastFailed = Base + 6;

    public const int ServerSettingsRefreshFailed = Base + 7;

    public const int ServerSettingsChangeRefreshFailed = Base + 8;

    public const int ServerSettingsChangeBroadcastFailed = Base + 9;

    public const int ServerSettingsReconciliationStarted = Base + 10;

    public const int ServerSettingsReconciled = Base + 11;

    public const int ServerSettingsReconciliationFailed = Base + 12;

    public const int ReconciledServerSettingsUnreadable = Base + 13;
}
