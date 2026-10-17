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

using System.Buffers;
using Microsoft.AspNetCore.DataProtection;
using NCode.Encoders;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Provides the default implementation of the <see cref="ISecretImporter"/> abstraction: a thin persistence wrapper
/// that resolves the <see cref="ISecretKeyTypeDescriptor"/> for the requested secret type (the single extensibility
/// seam), delegates parsing and validation of the caller-supplied public material to it, and assembles the
/// data-protected <see cref="PersistedSecret"/>.
/// </summary>
// See ADR-0104.
internal sealed class DefaultSecretImporter(
    IDataProtectorFactory<PersistedSecret> dataProtectorFactory,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : ISecretImporter
{
    private IDataProtector DataProtector { get; } = dataProtectorFactory.CreateDataProtector();

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public PersistedSecret ImportSecret(SecretKeyImportRequest request)
    {
        if (
            !SecretKeyTypeCatalog.TryGet(request.SecretType, out var descriptor)
            || !descriptor.SupportsPublicKeyImport
        )
        {
            throw new ArgumentException(
                $"The secret type '{request.SecretType}' does not support importing public key material.",
                nameof(request)
            );
        }

        var material = descriptor.ImportPublicKeyMaterial(
            new SecretKeyImportParameters
            {
                Format = request.Format,
                KeyMaterial = request.KeyMaterial,
            },
            Protect
        );

        return new PersistedSecret
        {
            SecretId = request.SecretId,
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

    private string Protect(ReadOnlySpan<byte> publicKeyBytes)
    {
        var writer = new ArrayBufferWriter<byte>();
        DataProtector.ProtectSpan(publicKeyBytes, ref writer);
        return Base64Url.Encode(writer.WrittenSpan);
    }
}
