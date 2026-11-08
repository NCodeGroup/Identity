// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Common abstraction for all cryptographic key material.
/// </summary>
/// <remarks>
/// All private key material should be encrypted at rest.
/// When requested, the private key material should be pinned while in use and securely zeroed after use.
/// </remarks>
[PublicAPI]
public abstract class SecretKey
{
    private string? ToStringOrNull { get; set; }

    /// <summary>
    /// Gets the type for this secret key.
    /// </summary>
    public abstract string KeyType { get; }

    /// <summary>
    /// Gets the metadata for this secret key.
    /// </summary>
    public abstract KeyMetadata Metadata { get; }

    /// <summary>
    /// Gets the <c>Key ID (KID)</c> for this secret key.
    /// </summary>
    public virtual string? KeyId => Metadata.KeyId;

    /// <summary>
    /// Gets the size, in bits, of the key material.
    /// For asymmetric keys, this is the size of the modulus.
    /// </summary>
    public abstract int KeySizeBits { get; }

    /// <summary>
    /// Gets the size, in bytes, of the key material.
    /// For asymmetric keys, this is the size of the modulus.
    /// For symmetric keys, this is the count of the bytes.
    /// </summary>
    public virtual int KeySizeBytes => (KeySizeBits + 7) >> 3;

    /// <summary>
    /// Returns a formatted <see cref="string"/> that represents this <see cref="SecretKey"/> instance.
    /// </summary>
    public override string ToString() => ToStringOrNull ??= Format();

    /// <summary>
    /// Appends the members of this <see cref="SecretKey"/> instance that <see cref="ToString"/> writes between its braces.
    /// A derived class overrides this method to append its own members after the base members, each preceded by <c>, </c>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> that receives the members.</param>
    protected virtual void AppendMembers(StringBuilder builder) =>
        builder.Append(
            CultureInfo.InvariantCulture,
            $"KeyId = {QuoteOrNull(KeyId)}, Size = {KeySizeBits}"
        );

    private string Format()
    {
        var builder = new StringBuilder();
        builder.Append(KeyType).Append(" { ");
        AppendMembers(builder);
        return builder.Append(" }").ToString();
    }

    /// <summary>
    /// Returns a quoted <see cref="string"/> or <c>(null)</c> if the value is <c>null</c>.
    /// </summary>
    /// <param name="value">The <see cref="string"/> value to quote.</param>
    /// <returns>The quoted value or <c>null</c>.</returns>
    protected static string QuoteOrNull(string? value) => value is null ? "(null)" : $"'{value}'";
}
