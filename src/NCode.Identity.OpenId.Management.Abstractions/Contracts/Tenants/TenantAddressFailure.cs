// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents one reason a tenant cannot be served at a proposed address, as reported by
/// <see cref="TenantAddressResource.Failures"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantAddressFailure
{
    /// <summary>
    /// Gets a message describing the failure.
    /// </summary>
    public required string Detail { get; init; }

    /// <summary>
    /// Gets a value indicating whether the failure is a conflict with another tenant — an address another tenant already
    /// holds, which a write answers with <c>409</c> — rather than a value that is invalid on its own (<c>400</c>).
    /// </summary>
    public required bool IsConflict { get; init; }
}
