#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Provides the common base for <see cref="ISecretKeyTypeDescriptor"/> implementations, deriving the
/// asymmetric/public-key flags from the descriptor's <see cref="KeyClrType"/> and sharing the <c>PKCS#8</c>
/// export-and-protect routine used by the asymmetric generators. This base takes no dependencies, so a custom key
/// type that constructs its own <see cref="SecretKey"/> can derive from it directly; a descriptor that needs the
/// low-level construction primitives derives from <see cref="AsymmetricSecretKeyTypeDescriptor"/> (which injects the
/// <see cref="ISecretKeyFactory"/>) instead.
/// </summary>
[PublicAPI]
public abstract class SecretKeyTypeDescriptor : ISecretKeyTypeDescriptor
{
    /// <inheritdoc />
    public abstract string KeyType { get; }

    /// <inheritdoc />
    public abstract Type KeyClrType { get; }

    /// <inheritdoc />
    public bool IsAsymmetric => typeof(AsymmetricSecretKey).IsAssignableFrom(KeyClrType);

    /// <inheritdoc />
    public virtual bool SupportsPublicKeyExport => IsAsymmetric;

    /// <inheritdoc />
    public virtual bool SupportsPublicKeyImport => IsAsymmetric;

    /// <inheritdoc />
    public abstract string? ValidateGeneration(SecretKeyGenerationParameters parameters);

    /// <inheritdoc />
    public abstract GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    );

    /// <inheritdoc />
    public virtual ImportedSecretKeyMaterial ImportPublicKeyMaterial(
        SecretKeyImportParameters parameters,
        SecretKeyMaterialProtector protect
    ) =>
        throw new NotSupportedException(
            $"The '{KeyType}' key type does not support importing public key material."
        );

    /// <inheritdoc />
    public abstract SecretKey DeserializeKeyMaterial(
        KeyMetadata metadata,
        string secretEncoding,
        ReadOnlySpan<byte> keyMaterial
    );

    /// <inheritdoc />
    public virtual ExportedPublicKey? ExportPublicKey(SecretKey secretKey) => null;

    /// <summary>
    /// Exports the <c>PKCS#8</c> private key of the specified asymmetric algorithm into a rented sensitive buffer
    /// and protects it via <paramref name="protect"/>, growing the buffer until the export succeeds.
    /// </summary>
    protected static GeneratedSecretKeyMaterial ExportProtectedPkcs8(
        AsymmetricAlgorithm algorithm,
        SecretKeyMaterialProtector protect
    )
    {
        var byteCount = 4096;
        while (true)
        {
            using var _ = BufferFactory.Rent(byteCount, isSensitive: true, out Span<byte> span);
            if (algorithm.TryExportPkcs8PrivateKey(span, out var bytesWritten))
            {
                return new GeneratedSecretKeyMaterial
                {
                    ProtectedKeyMaterial = protect(span[..bytesWritten]),
                    KeySizeBits = algorithm.KeySize,
                    SecretEncoding = SecretEncodings.Pkcs8,
                    SecretMaterial = null,
                };
            }

            byteCount = checked(byteCount * 2);
        }
    }

    /// <summary>
    /// Returns a rejection reason when a curve is requested for a key type that does not use elliptic curves, or
    /// <c>null</c> when no curve was requested.
    /// </summary>
    protected static string? RejectCurveIfPresent(SecretKeyGenerationParameters parameters) =>
        parameters.Curve is not null
            ? "The 'curve' parameter is only valid for elliptic-curve (ecc) secrets."
            : null;
}
