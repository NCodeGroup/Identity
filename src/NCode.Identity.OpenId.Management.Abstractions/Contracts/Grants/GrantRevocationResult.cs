// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Grants;

/// <summary>
/// Represents the result of a bulk grant revocation, reporting how many grants were revoked.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GrantRevocationResult
{
    /// <summary>
    /// Gets the number of grants that were revoked by the operation. Already-revoked grants are not counted.
    /// </summary>
    public required long Revoked { get; init; }
}
