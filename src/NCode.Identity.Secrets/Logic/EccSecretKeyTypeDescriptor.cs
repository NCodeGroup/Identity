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
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// The built-in <see cref="ISecretKeyTypeDescriptor"/> for <see cref="SecretKeyTypes.Ecc"/> keys. The supported
/// curves come from the injected <see cref="IEccCurveCatalog"/> (the single, extensible source of truth), so a
/// caller may request a specific curve by name or let one be selected by key size. ECC keys are asymmetric and
/// therefore expose exportable and importable public-key material.
/// </summary>
// See ADR-0068.
internal sealed class EccSecretKeyTypeDescriptor(
    ISecretKeyFactory secretKeyFactory,
    IEccCurveCatalog eccCurveCatalog
) : AsymmetricSecretKeyTypeDescriptor(secretKeyFactory)
{
    private IEccCurveCatalog EccCurveCatalog { get; } = eccCurveCatalog;

    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Ecc;

    /// <inheritdoc />
    public override Type KeyClrType => typeof(EccSecretKey);

    /// <inheritdoc />
    protected override string ExpectedJwkKeyType => "EC";

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

    /// <inheritdoc />
    protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ParseJwkPublicKey(
        JsonElement root
    )
    {
        if (
            !root.TryGetProperty("crv", out var crvElement)
            || crvElement.ValueKind != JsonValueKind.String
        )
            throw new ArgumentException("The supplied EC JWK is missing the 'crv' member.");

        var curveName = crvElement.GetString();
        if (
            string.IsNullOrEmpty(curveName)
            || !EccCurveCatalog.TryGetByName(curveName, out var specification)
        )
            throw new ArgumentException($"The elliptic curve '{curveName}' is not supported.");

        var parameters = new ECParameters
        {
            Curve = specification.Curve,
            Q = new ECPoint
            {
                X = GetRequiredBase64UrlMember(root, "x"),
                Y = GetRequiredBase64UrlMember(root, "y"),
            },
        };

        try
        {
            using var ecdsa = ECDsa.Create(parameters);
            return (ecdsa.ExportSubjectPublicKeyInfo(), ecdsa.KeySize);
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException("The supplied EC JWK is malformed.", exception);
        }
    }

    /// <inheritdoc />
    protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ImportPublicKeyPem(string pem)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return (ecdsa.ExportSubjectPublicKeyInfo(), ecdsa.KeySize);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "The supplied public key PEM is not a valid elliptic-curve key.",
                exception
            );
        }
    }

    /// <inheritdoc />
    protected override bool TryGetCertificateKeySizeBits(
        X509Certificate2 certificate,
        out int keySizeBits
    )
    {
        using var ecdsa = certificate.GetECDsaPublicKey();
        keySizeBits = ecdsa?.KeySize ?? 0;
        return ecdsa is not null;
    }

    /// <inheritdoc />
    protected override SecretKey CreatePrivateKeyFromPkcs8(
        KeyMetadata metadata,
        ReadOnlySpan<byte> pkcs8
    ) => SecretKeyFactory.CreateEccPkcs8(metadata, pkcs8);

    /// <inheritdoc />
    protected override SecretKey CreatePublicKeyFromSpki(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo
    ) => SecretKeyFactory.CreateEccSpki(metadata, subjectPublicKeyInfo);

    /// <inheritdoc />
    protected override JsonObject? WritePublicKeyJwk(AsymmetricSecretKey secretKey)
    {
        using var ecdsa = ((EccSecretKey)secretKey).ExportECDsa();
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        // Resolve the JWK 'crv' by the key's actual curve so two same-size curves are named correctly, falling
        // back to the curve size; a key whose curve is not in the catalog is omitted (null) rather than guessed.
        if (
            !EccCurveCatalog.TryGetByCurve(parameters.Curve, out var specification)
            && !EccCurveCatalog.TryGetByCurveSizeBits(secretKey.KeySizeBits, out specification)
        )
        {
            return null;
        }

        return new JsonObject
        {
            ["kty"] = "EC",
            ["crv"] = specification.CurveName,
            ["x"] = parameters.Q.X is { } x
                ? Base64Url.EncodeToString(x)
                : throw new InvalidOperationException("The ECC key is missing its X coordinate."),
            ["y"] = parameters.Q.Y is { } y
                ? Base64Url.EncodeToString(y)
                : throw new InvalidOperationException("The ECC key is missing its Y coordinate."),
        };
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
