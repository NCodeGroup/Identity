// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to rotate a client secret: the server generates a replacement of the same type, size,
/// use, and algorithm, and removes the rotated secret in the same change.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RotateSecretRequest
{
    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the replacement secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
