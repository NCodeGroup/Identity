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
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// The built-in <see cref="ISecretKeyTypeDescriptor"/> for <see cref="SecretKeyTypes.Rsa"/> keys. RSA keys are
/// asymmetric and therefore expose exportable public-key material.
/// </summary>
internal sealed class RsaSecretKeyTypeDescriptor : SecretKeyTypeDescriptor
{
    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Rsa;

    /// <inheritdoc />
    public override Type KeyClrType => typeof(RsaSecretKey);

    /// <inheritdoc />
    public override string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
        RejectCurveIfPresent(parameters) ?? ValidateKeySizeBits(parameters.KeySizeBits);

    private static string? ValidateKeySizeBits(int keySizeBits) =>
        keySizeBits < 2048 || keySizeBits > 16384 || keySizeBits % 8 != 0
            ? $"The RSA key size '{keySizeBits}' bits is invalid; expected a multiple of 8 "
                + "between 2048 and 16384 bits."
            : null;

    /// <inheritdoc />
    public override GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        using var rsa = RSA.Create(parameters.KeySizeBits);
        return ExportProtectedPkcs8(rsa, protect);
    }
}
