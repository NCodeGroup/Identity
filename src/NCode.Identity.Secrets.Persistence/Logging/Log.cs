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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Secrets.Persistence</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    /// <summary>
    /// Logs that a new master key was created and persisted.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the master key.</param>
    /// <param name="version">The version of the master key.</param>
    /// <param name="rootKeyProvider">The name of the root key provider that wrapped the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyCreated,
        Level = LogLevel.Information,
        Message = "Created master key {KeyId} (version {Version}) for '{Owner}' wrapped by the root key provider '{RootKeyProvider}'"
    )]
    internal static partial void MasterKeyCreated(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId,
        int version,
        string rootKeyProvider
    );

    /// <summary>
    /// Logs that an existing master key was unwrapped by its root key provider.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the master key.</param>
    /// <param name="rootKeyProvider">The name of the root key provider that unwrapped the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyUnwrapped,
        Level = LogLevel.Debug,
        Message = "Unwrapped master key {KeyId} for '{Owner}' with the root key provider '{RootKeyProvider}'"
    )]
    internal static partial void MasterKeyUnwrapped(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId,
        string rootKeyProvider
    );

    /// <summary>
    /// Logs that another node created the master key for an owner first, so the stored key is used instead.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyCreationRaceLost,
        Level = LogLevel.Debug,
        Message = "Another node created the master key for '{Owner}' first; using the stored master key"
    )]
    internal static partial void MasterKeyCreationRaceLost(this ILogger logger, SecretOwner owner);

    /// <summary>
    /// Logs that master keys are persisted without being wrapped by a recovery key.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(
        EventId = EventIds.RecoveryWrapDisabled,
        Level = LogLevel.Warning,
        Message = "Master keys are not wrapped by a recovery key; losing the root key loses every stored secret."
    )]
    internal static partial void RecoveryWrapDisabled(this ILogger logger);
}
