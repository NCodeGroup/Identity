// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains a protected value that a record stores, together with what a rewrap needs to protect it again under the
/// same context and to write it back: the identifier of the record, the identifier of the item that the value is bound
/// to, and the concurrency token of the record. The protected value is an envelope and is not secret.
/// </summary>
// See ADR-1407.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedProtectedValue
{
    /// <summary>
    /// Gets the identifier of the record that stores the value, such as a secret identifier or a grant identifier.
    /// </summary>
    public required string RecordId { get; init; }

    /// <summary>
    /// Gets the identifier of the item that the value is bound to, which is the item identifier of the
    /// protection context: a secret identifier or the hashed key of a grant.
    /// </summary>
    public required string ItemId { get; init; }

    /// <summary>
    /// Gets or sets the protected value.
    /// </summary>
    public required string ProtectedValue { get; set; }

    /// <summary>
    /// Gets the concurrency token of the record, which the update checks.
    /// </summary>
    public required string ConcurrencyToken { get; init; }
}
