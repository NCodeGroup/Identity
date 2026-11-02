// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.AuditRecords;

/// <summary>
/// Represents an entity an audited operation acted on.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AuditRecordResourceReference
{
    /// <summary>
    /// Gets the type of the resource, such as <c>client</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the identifier of the resource, or <c>null</c>.
    /// </summary>
    public string? Id { get; init; }
}
