// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Describes what <see cref="IInstanceSetupService"/> creates in one setup: the root administrator and, optionally,
/// the management console's client.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class InstanceSetupRequest
{
    /// <summary>
    /// Gets the root administrator to create.
    /// </summary>
    public required SetupRootOptions Root { get; init; }

    /// <summary>
    /// Gets the management console's client to register, or <c>null</c> when setup registers no console client.
    /// </summary>
    public SetupManagementConsoleOptions? ManagementConsole { get; init; }
}
