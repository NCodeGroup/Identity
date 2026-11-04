// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Contains common metadata for a secret key such as <c>KeyId</c>, <c>Use</c>, <c>Algorithm</c>, and the instants of
/// its lifecycle: <c>ActivatesWhen</c>, <c>RetiresWhen</c> and <c>ExpiresWhen</c>.
/// </summary>
/// <remarks>
/// <para>
/// This structure holds the standard metadata properties defined in the JSON Web Key (JWK) specification
/// (RFC 7517) that describe how a cryptographic key should be identified and used, and the instants that bound the
/// key's active window (when it signs or encrypts) and its validity window (when it verifies, decrypts and is
/// published).
/// </para>
/// <para>
/// All properties are optional and nullable, allowing for flexible key configurations where
/// metadata may be partially specified or omitted entirely.
/// </para>
/// </remarks>
/// <seealso href="https://tools.ietf.org/html/rfc7517">RFC 7517 - JSON Web Key (JWK)</seealso>
[PublicAPI]
public readonly record struct KeyMetadata
{
    /// <summary>
    /// Gets the <c>Key ID (kid)</c> for the secret key.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies the key, or <c>null</c> if no identifier is specified.
    /// </value>
    /// <remarks>
    /// The Key ID is used to match a specific key when multiple keys are available.
    /// This corresponds to the <c>kid</c> parameter defined in RFC 7517 Section 4.5.
    /// </remarks>
    /// <seealso href="https://tools.ietf.org/html/rfc7517#section-4.5">RFC 7517 Section 4.5</seealso>
    public string? KeyId { get; init; }

    /// <summary>
    /// Gets the intended use for the secret key.
    /// </summary>
    /// <value>
    /// A string indicating the intended use of the key, or <c>null</c> to indicate the key
    /// may be used with any compatible operation.
    /// </value>
    /// <remarks>
    /// <para>
    /// This corresponds to the <c>use</c> parameter defined in RFC 7517 Section 4.2.
    /// Common values include:
    /// </para>
    /// <list type="bullet">
    ///     <item><description><c>sig</c> — The key is intended for digital signatures.</description></item>
    ///     <item><description><c>enc</c> — The key is intended for encryption.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso href="https://tools.ietf.org/html/rfc7517#section-4.2">RFC 7517 Section 4.2</seealso>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the secret key.
    /// </summary>
    /// <value>
    /// A string identifying the algorithm intended for use with the key, or <c>null</c> to indicate
    /// the key may be used with any compatible algorithm.
    /// </value>
    /// <remarks>
    /// <para>
    /// This corresponds to the <c>alg</c> parameter defined in RFC 7517 Section 4.4.
    /// When specified, this identifies the specific cryptographic algorithm the key is intended for
    /// (e.g., <c>RS256</c>, <c>ES384</c>, <c>A128GCM</c>).
    /// </para>
    /// </remarks>
    /// <seealso href="https://tools.ietf.org/html/rfc7517#section-4.4">RFC 7517 Section 4.4</seealso>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets the instant the key's active window opens: from then on the key signs or encrypts, so a key that is
    /// published ahead of its use carries a future value.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the key starts to sign or encrypt, or <c>null</c> to indicate
    /// the key is active from the moment it exists.
    /// </value>
    public DateTimeOffset? ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the instant the key's active window closes: from then on the key signs and encrypts nothing, while it
    /// still verifies and decrypts until it expires.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the key stops signing or encrypting, or <c>null</c> to indicate
    /// the key signs or encrypts until it expires.
    /// </value>
    public DateTimeOffset? RetiresWhen { get; init; }

    /// <summary>
    /// Gets the expiration time for the secret key: the instant its validity window closes, after which it neither
    /// verifies nor decrypts and is not published.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the key expires and is no longer valid,
    /// or <c>null</c> to indicate the key never expires.
    /// </value>
    /// <remarks>
    /// An expired key is not used for any cryptographic operation. A key's validity window
    /// (<see cref="ExpiresWhen"/>) contains its active window (<see cref="ActivatesWhen"/> to
    /// <see cref="RetiresWhen"/>), so a retired key still verifies what it signed until it expires.
    /// </remarks>
    public DateTimeOffset? ExpiresWhen { get; init; }
}
