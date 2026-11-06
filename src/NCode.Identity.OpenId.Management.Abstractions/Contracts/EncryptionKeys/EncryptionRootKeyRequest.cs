// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Represents the request body that sets or changes the root key of a tenant.
/// </summary>
// See ADR-1414.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EncryptionRootKeyRequest
{
    /// <summary>
    /// Gets the name of the key wrapper that wraps under the key, one of the providers that the tenant's encryption keys
    /// resource lists.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the reference to the key, such as the ARN of a KMS key.
    /// </summary>
    public required string KeyReference { get; init; }

    /// <summary>
    /// Gets the identity that the server takes on to reach the key, such as an IAM role ARN, a service account email or
    /// a Microsoft Entra tenant ID; when omitted, the deployment's own identity reaches the key.
    /// </summary>
    public string? AccessIdentity { get; init; }
}
