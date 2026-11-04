// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to update the metadata of an existing server secret. Key material is immutable
/// once generated; to replace it, create a new one and delete the old one, or rotate a client secret.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateSecretRequest
{
    /// <summary>
    /// Gets the intended use for the secret, or <c>null</c> for any compatible use.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret starts to sign or encrypt, or <c>null</c> to activate it
    /// from its creation. Never before the creation.
    /// </summary>
    public DateTimeOffset? ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret stops signing or encrypting while it still verifies or
    /// decrypts, or <c>null</c> to retire it when it expires. Never before <see cref="ActivatesWhen"/>, never after
    /// <see cref="ExpiresWhen"/>.
    /// </summary>
    public DateTimeOffset? RetiresWhen { get; init; }
}
