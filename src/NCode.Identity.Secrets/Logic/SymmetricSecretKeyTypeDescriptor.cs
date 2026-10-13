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

using System.Buffers.Text;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// The built-in <see cref="ISecretKeyTypeDescriptor"/> for <see cref="SecretKeyTypes.Symmetric"/> keys. Symmetric
/// keys have no public key, so <see cref="ISecretKeyTypeDescriptor.SupportsPublicKeyExport"/> is <c>false</c>;
/// their generated material is the one-time reveal value.
/// </summary>
// See ADR-0050.
internal sealed class SymmetricSecretKeyTypeDescriptor : SecretKeyTypeDescriptor
{
    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Symmetric;

    /// <inheritdoc />
    public override Type KeyClrType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
        RejectCurveIfPresent(parameters) ?? ValidateKeySizeBits(parameters.KeySizeBits);

    private static string? ValidateKeySizeBits(int keySizeBits) =>
        keySizeBits < 128 || keySizeBits % 8 != 0
            ? $"The symmetric key size '{keySizeBits}' bits is invalid; expected a multiple of 8 "
                + "that is at least 128 bits."
            : null;

    /// <inheritdoc />
    public override GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        var keySizeBits = parameters.KeySizeBits;
        if (keySizeBits <= 0 || keySizeBits % 8 != 0)
        {
            throw new ArgumentException(
                "The symmetric key size (in bits) must be a positive multiple of 8.",
                nameof(parameters)
            );
        }

        var keySizeBytes = keySizeBits >> 3;
        using var _ = BufferFactory.Rent(keySizeBytes, isSensitive: true, out Span<byte> span);
        RandomNumberGenerator.Fill(span);

        // Reveal-once plaintext for symmetric shared secrets (ADR-0050); asymmetric private keys stay null.
        var secretMaterial = Base64Url.EncodeToString(span);

        return new GeneratedSecretKeyMaterial
        {
            ProtectedKeyMaterial = protect(span),
            KeySizeBits = keySizeBits,
            SecretMaterial = secretMaterial,
        };
    }
}
