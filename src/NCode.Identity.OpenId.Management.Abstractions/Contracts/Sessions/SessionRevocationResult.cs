// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Sessions;

/// <summary>
/// Represents the result of a bulk session revocation, reporting how many sessions were revoked.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SessionRevocationResult
{
    /// <summary>
    /// Gets the number of sessions that were revoked by the operation.
    /// </summary>
    public required long Revoked { get; init; }
}
