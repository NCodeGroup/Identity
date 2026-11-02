// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Encoders;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the default implementation of the <see cref="ISecretImporter"/> abstraction: a thin persistence wrapper
/// that resolves the <see cref="ISecretKeyTypeDescriptor"/> for the requested secret type (the single extensibility
/// seam), delegates parsing and validation of the caller-supplied public material to it, and assembles the
/// <see cref="PersistedSecret"/>. The imported material is public, so it is stored as plain base64url without
/// encryption because it is not secret; an importer never stores private material.
/// </summary>
// See ADR-1104.
// See ADR-1235.
internal sealed class DefaultSecretImporter(ISecretKeyTypeCatalog secretKeyTypeCatalog)
    : ISecretImporter
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public PersistedSecret ImportSecret(SecretKeyImportRequest request)
    {
        if (
            !SecretKeyTypeCatalog.TryGet(request.SecretType, out var descriptor)
            || descriptor.PublicKeyImportFormats.Count == 0
        )
        {
            throw new ArgumentException(
                $"The secret type '{request.SecretType}' does not support importing public key material.",
                nameof(request)
            );
        }

        if (!descriptor.PublicKeyImportFormats.Contains(request.Format))
        {
            throw new ArgumentException(
                $"The secret type '{request.SecretType}' does not import public key material in the "
                    + $"'{request.Format}' format.",
                nameof(request)
            );
        }

        var material = descriptor.ImportPublicKeyMaterial(
            new SecretKeyImportParameters
            {
                Format = request.Format,
                KeyMaterial = request.KeyMaterial,
            },
            static bytes => Base64Url.Encode(bytes)
        );

        if (!SecretEncodings.IsPublic(material.SecretEncoding))
        {
            throw new InvalidOperationException(
                $"The secret type '{request.SecretType}' produced the '{material.SecretEncoding}' encoding, "
                    + "which is not public key material and cannot be imported."
            );
        }

        return new PersistedSecret
        {
            SecretId = request.SecretId,
            KeyId = request.KeyId,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = request.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = request.SecretType,
            SecretEncoding = material.SecretEncoding,
            KeySizeBits = material.KeySizeBits,
            EncodedValue = material.ProtectedKeyMaterial,
        };
    }
}
