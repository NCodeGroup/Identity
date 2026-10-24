#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.Secrets.Persistence.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.Secrets.Persistence</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>7000</c>–<c>7999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 7000;

    /// <summary>
    /// A new master key was created and persisted.
    /// </summary>
    public const int MasterKeyCreated = Base + 1;

    /// <summary>
    /// An existing master key was unwrapped by its root key provider.
    /// </summary>
    public const int MasterKeyUnwrapped = Base + 2;

    /// <summary>
    /// Another node won the race to create the master key for an owner, so this node used the stored key.
    /// </summary>
    public const int MasterKeyCreationRaceLost = Base + 3;

    /// <summary>
    /// Master keys are persisted without being wrapped by a recovery key.
    /// </summary>
    public const int RecoveryWrapDisabled = Base + 4;
}
