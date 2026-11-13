// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the management console that the deployment names for the wizard's console step. It is only a hint: it
/// registers nothing.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetupManagementConsoleHint
{
    /// <summary>
    /// Gets the https origin of the management console.
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Gets the <c>client_id</c> that the console step proposes for the management console's client.
    /// </summary>
    public required string ClientId { get; init; }
}
