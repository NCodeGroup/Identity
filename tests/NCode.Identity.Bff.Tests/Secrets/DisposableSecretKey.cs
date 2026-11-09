// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Bff.Tests.Secrets;

/// <summary>
/// A secret key that records its disposal, for the tests of code that disposes the keys it reads.
/// </summary>
internal sealed class DisposableSecretKey(KeyMetadata metadata) : SecretKey, IDisposable
{
    /// <inheritdoc />
    public override string KeyType => "test";

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override int KeySizeBits => 256;

    /// <summary>
    /// Gets a value that indicates whether the key is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;
}
