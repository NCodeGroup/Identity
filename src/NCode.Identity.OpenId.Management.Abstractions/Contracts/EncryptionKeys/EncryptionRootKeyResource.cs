// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Represents the REST resource that describes the root key a tenant brings: which key it is and the state of its last
/// validation. It carries no key material.
/// </summary>
// See ADR-1411.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EncryptionRootKeyResource
{
    /// <summary>
    /// Gets the name of the key wrapper that wraps under the key, such as <c>aws-kms</c>.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the reference to the key that the tenant gave, such as the ARN of a KMS key.
    /// </summary>
    public required string KeyReference { get; init; }

    /// <summary>
    /// Gets the state of the key: one of the values in <see cref="EncryptionRootKeyStates"/>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the instant the key reference was set.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the instant the state last changed.
    /// </summary>
    public required DateTimeOffset StateChangedWhen { get; init; }

    /// <summary>
    /// Gets the instant of the last successful validation, or <c>null</c> when none has succeeded.
    /// </summary>
    public DateTimeOffset? ValidatedWhen { get; init; }

    /// <summary>
    /// Gets the instant of the last failed validation, or <c>null</c> when none has failed.
    /// </summary>
    public DateTimeOffset? FailedWhen { get; init; }

    /// <summary>
    /// Gets the description of the last failure, or <c>null</c> after a success.
    /// </summary>
    public string? Failure { get; init; }
}
