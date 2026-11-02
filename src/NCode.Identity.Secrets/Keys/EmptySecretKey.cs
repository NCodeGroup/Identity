// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides an <see cref="SecretKey"/> implementation that is empty.
/// </summary>
internal sealed class EmptySecretKey : SecretKey
{
    /// <summary>
    /// Gets a singleton instance of <see cref="EmptySecretKey"/>.
    /// </summary>
    public static EmptySecretKey Singleton { get; } = new();

    /// <inheritdoc />
    public override string KeyType => string.Empty;

    /// <inheritdoc />
    public override KeyMetadata Metadata => default;

    /// <inheritdoc />
    public override int KeySizeBits => 0;
}
