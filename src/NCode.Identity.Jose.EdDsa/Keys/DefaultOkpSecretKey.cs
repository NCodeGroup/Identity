// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Keys;

/// <summary>
/// An <see cref="OkpSecretKey"/> that holds the private key, data-protected at rest in memory. On a signing curve it
/// signs and verifies; on a key-agreement curve it agrees keys as either party.
/// </summary>
internal sealed class DefaultOkpSecretKey(
    IDataProtector dataProtector,
    KeyMetadata metadata,
    OkpCurve curve,
    ReadOnlyMemory<byte> protectedSeed,
    ReadOnlyMemory<byte> publicKey
) : OkpSecretKey(metadata, curve.Name, curve.KeySizeBits, publicKey, certificateRawData: null)
{
    private IDataProtector DataProtector { get; } = dataProtector;
    private ReadOnlyMemory<byte> ProtectedSeed { get; } = protectedSeed;

    /// <inheritdoc />
    public override bool HasPrivateKey => true;

    /// <inheritdoc />
    public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
        DataProtector.UnprotectSpan(ProtectedSeed.Span, ref destination);
}
