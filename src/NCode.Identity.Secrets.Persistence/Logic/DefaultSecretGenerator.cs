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
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using NCode.Encoders;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretGenerator"/> abstraction that generates key
/// material server-side and data-protects it using the same protector <see cref="DefaultSecretSerializer"/>
/// uses to unprotect it. The per-type generation logic is delegated to the registered
/// <see cref="ISecretKeyTypeDescriptor"/> for the requested key type.
/// </summary>
// See ADR-0067.
internal class DefaultSecretGenerator(
    IDataProtectorFactory<PersistedSecret> dataProtectorFactory,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : ISecretGenerator
{
    private IDataProtector DataProtector { get; } = dataProtectorFactory.CreateDataProtector();

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public GeneratedSecret GenerateSecret(GenerateSecretRequest request)
    {
        if (!SecretKeyTypeCatalog.TryGet(request.SecretType, out var descriptor))
        {
            throw new InvalidOperationException(
                $"The '{request.SecretType}' secret type cannot be generated."
            );
        }

        var material = descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters
            {
                KeySizeBits = request.KeySizeBits,
                Curve = request.Curve,
            },
            Protect
        );

        var secret = new PersistedSecret
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

        return new GeneratedSecret { Secret = secret, SecretMaterial = material.SecretMaterial };
    }

    /// <inheritdoc />
    public GeneratedSecret GenerateSharedSecret(GenerateSecretRequest request)
    {
        if (request.SecretType != SecretTypes.Symmetric)
        {
            throw new InvalidOperationException(
                $"A shared secret must be of type '{SecretTypes.Symmetric}'."
            );
        }

        if (request.KeySizeBits <= 0 || request.KeySizeBits % 8 != 0)
        {
            throw new ArgumentException(
                "The shared-secret entropy size (in bits) must be a positive multiple of 8.",
                nameof(request)
            );
        }

        // The opaque secret string is what an external party holds and later presents; its UTF-8 bytes are stored as
        // the symmetric key material so a UTF-8 comparison of the presented secret against the stored material matches.
        var entropyByteLength = request.KeySizeBits >> 3;
        var entropy = new byte[entropyByteLength];
        RandomNumberGenerator.Fill(entropy);
        var secretMaterial = Base64Url.Encode(entropy);

        var materialBytes = Encoding.UTF8.GetBytes(secretMaterial);
        var secret = new PersistedSecret
        {
            SecretId = request.SecretId,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = request.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = SecretEncodings.SymmetricRaw,
            KeySizeBits = materialBytes.Length * 8,
            EncodedValue = Protect(materialBytes),
        };

        return new GeneratedSecret { Secret = secret, SecretMaterial = secretMaterial };
    }

    private string Protect(ReadOnlySpan<byte> privateKeyBytes)
    {
        var writer = new ArrayBufferWriter<byte>();
        DataProtector.ProtectSpan(privateKeyBytes, ref writer);
        return Base64Url.Encode(writer.WrittenSpan);
    }
}
