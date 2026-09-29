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
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretGenerator"/> abstraction that generates key
/// material server-side and data-protects it using the same protector <see cref="DefaultSecretSerializer"/>
/// uses to unprotect it.
/// </summary>
internal class DefaultSecretGenerator(IDataProtectorFactory<PersistedSecret> dataProtectorFactory)
    : ISecretGenerator
{
    private IDataProtector DataProtector { get; } = dataProtectorFactory.CreateDataProtector();

    /// <inheritdoc />
    public PersistedSecret GenerateSecret(GenerateSecretRequest request)
    {
        var (encodedValue, keySizeBits) = request.SecretType switch
        {
            SecretTypes.Symmetric => GenerateSymmetric(request.KeySizeBits),
            SecretTypes.Rsa => GenerateRsa(request.KeySizeBits),
            SecretTypes.Ecc => GenerateEcc(request.KeySizeBits),
            _ => throw new InvalidOperationException(
                $"The '{request.SecretType}' secret type cannot be generated."
            ),
        };

        return new PersistedSecret
        {
            SecretId = request.SecretId,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = request.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = request.SecretType,
            KeySizeBits = keySizeBits,
            EncodedValue = encodedValue,
        };
    }

    private (string EncodedValue, int KeySizeBits) GenerateSymmetric(int keySizeBits)
    {
        if (keySizeBits <= 0 || keySizeBits % 8 != 0)
        {
            throw new ArgumentException(
                "The symmetric key size (in bits) must be a positive multiple of 8.",
                nameof(keySizeBits)
            );
        }

        var keySizeBytes = keySizeBits >> 3;
        using var _ = BufferFactory.Rent(keySizeBytes, isSensitive: true, out Span<byte> span);
        RandomNumberGenerator.Fill(span);

        return (Protect(span), keySizeBits);
    }

    private (string EncodedValue, int KeySizeBits) GenerateRsa(int keySizeBits)
    {
        using var rsa = RSA.Create(keySizeBits);
        return (ExportProtectedPkcs8(rsa), rsa.KeySize);
    }

    private (string EncodedValue, int KeySizeBits) GenerateEcc(int keySizeBits)
    {
        var curve = keySizeBits switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new ArgumentException(
                $"The ECC key size '{keySizeBits}' bits is not supported; expected 256, 384, or 521.",
                nameof(keySizeBits)
            ),
        };

        using var ecdsa = ECDsa.Create(curve);
        return (ExportProtectedPkcs8(ecdsa), ecdsa.KeySize);
    }

    private string ExportProtectedPkcs8(AsymmetricAlgorithm algorithm)
    {
        var byteCount = 4096;
        while (true)
        {
            using var _ = BufferFactory.Rent(byteCount, isSensitive: true, out Span<byte> span);
            if (algorithm.TryExportPkcs8PrivateKey(span, out var bytesWritten))
            {
                return Protect(span[..bytesWritten]);
            }

            byteCount = checked(byteCount * 2);
        }
    }

    private string Protect(ReadOnlySpan<byte> privateKeyBytes)
    {
        var writer = new ArrayBufferWriter<byte>();
        DataProtector.ProtectSpan(privateKeyBytes, ref writer);
        return Base64Url.Encode(writer.WrittenSpan);
    }
}
