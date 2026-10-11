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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// The built-in <see cref="ISecretKeyTypeDescriptor"/> for <see cref="SecretKeyTypes.Ecc"/> keys. The supported
/// curves come from the injected <see cref="IEccCurveCatalog"/> (the single, extensible source of truth), so a
/// caller may request a specific curve by name or let one be selected by key size (ADR-0068).
/// </summary>
internal sealed class EccSecretKeyTypeDescriptor(IEccCurveCatalog eccCurveCatalog)
    : SecretKeyTypeDescriptor
{
    private IEccCurveCatalog EccCurveCatalog { get; } = eccCurveCatalog;

    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Ecc;

    /// <inheritdoc />
    public override Type KeyClrType => typeof(EccSecretKey);

    /// <inheritdoc />
    public override string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
        TryResolveCurve(parameters, out _, out var error) ? null : error;

    /// <inheritdoc />
    public override GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        if (!TryResolveCurve(parameters, out var specification, out var error))
        {
            throw new ArgumentException(error, nameof(parameters));
        }

        using var ecdsa = ECDsa.Create(specification.Curve);
        return ExportProtectedPkcs8(ecdsa, protect);
    }

    // Resolves the requested curve by explicit name, else by key size, producing a human-readable error otherwise.
    private bool TryResolveCurve(
        SecretKeyGenerationParameters parameters,
        [NotNullWhen(true)] out EccCurveSpecification? specification,
        out string? error
    )
    {
        if (parameters.Curve is { } curveName)
        {
            if (EccCurveCatalog.TryGetByName(curveName, out var byName))
            {
                specification = byName;
                error = null;
                return true;
            }

            specification = null;
            error = $"The ECC curve '{curveName}' is not supported.";
            return false;
        }

        if (EccCurveCatalog.TryGetByCurveSizeBits(parameters.KeySizeBits, out var bySize))
        {
            specification = bySize;
            error = null;
            return true;
        }

        specification = null;
        error =
            $"The ECC key size '{parameters.KeySizeBits}' bits is invalid; expected a supported curve size.";
        return false;
    }
}
