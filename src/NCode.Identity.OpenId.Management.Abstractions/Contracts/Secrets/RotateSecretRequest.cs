// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to rotate a client secret: the server generates a replacement of the same type, use,
/// and algorithm and keeps the rotated secret, so both are accepted while its holder switches.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RotateSecretRequest
{
    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the replacement secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the rotated secret stops being accepted, or
    /// <see langword="null"/> to keep its current expiry. A value later than its current expiry is ignored, so
    /// rotation never extends the rotated secret.
    /// </summary>
    public DateTimeOffset? RotatedExpiresWhen { get; init; }
}
