// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides a default implementation for the <see cref="SymmetricSecretKey"/> abstraction.
/// </summary>
internal class DefaultSymmetricSecretKey(
    IDataProtector dataProtector,
    KeyMetadata metadata,
    int keySizeBytes,
    ReadOnlyMemory<byte> protectedPrivateKey
) : SymmetricSecretKey
{
    private IDataProtector DataProtector { get; } = dataProtector;
    private ReadOnlyMemory<byte> ProtectedPrivateKey { get; } = protectedPrivateKey;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override int KeySizeBits => KeySizeBytes << 3;

    /// <inheritdoc />
    public override int KeySizeBytes { get; } = keySizeBytes;

    /// <inheritdoc />
    public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
        DataProtector.UnprotectSpan(ProtectedPrivateKey.Span, ref destination);
}
