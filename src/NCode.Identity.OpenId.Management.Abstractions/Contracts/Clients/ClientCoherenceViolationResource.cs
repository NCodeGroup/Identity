// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents one relation a client's settings break: a value the client registers that an effective setting does not
/// permit, or a value an effective setting requires that the client does not register.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientCoherenceViolationResource
{
    /// <summary>
    /// Gets the name of the client setting that registers the value or that is required.
    /// </summary>
    public required string SettingName { get; init; }

    /// <summary>
    /// Gets the registered value that is not permitted, or an empty string when a required value is missing.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the name of the effective setting that does not permit the value or that requires it.
    /// </summary>
    public required string GoverningSettingName { get; init; }

    /// <summary>
    /// Gets the description of the violation, which names the settings and the values involved.
    /// </summary>
    public required string Description { get; init; }
}
