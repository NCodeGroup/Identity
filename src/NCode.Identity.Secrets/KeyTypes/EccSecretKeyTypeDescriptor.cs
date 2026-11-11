// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.Json;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// The built-in <see cref="ISecretKeyTypeDescriptor"/> for <see cref="SecretKeyTypes.Ecc"/> keys. The supported
/// curves come from the injected <see cref="IEccCurveCatalog"/> (the single, extensible source of truth), so a
/// caller may request a specific curve by name, with its key size, or let the key size select the one catalog curve
/// of that size; a size that two catalog curves share selects neither and is refused until the curve is named. ECC
/// keys are asymmetric and therefore expose exportable and importable public-key material.
/// </summary>
// See ADR-1068 and ADR-1398.
internal sealed class EccSecretKeyTypeDescriptor(
    ISecretKeyFactory secretKeyFactory,
    IEccCurveCatalog eccCurveCatalog
) : AsymmetricSecretKeyTypeDescriptor(secretKeyFactory), IRemoteSecretKeyTypeDescriptor
{
    private IEccCurveCatalog EccCurveCatalog { get; } = eccCurveCatalog;

    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Ecc;

    /// <inheritdoc />
    public override Type KeyClrType => typeof(EccSecretKey);

    /// <inheritdoc />
    public override JsonWebKeyTypeDescriptor JwkKeyType => JsonWebKeyTypeDescriptors.Ec;

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
        if (!root.TryGetPropertyValue<string>(JsonWebKeyParameterNames.Curve, out var curveName))
            throw new ArgumentException("The supplied EC JWK is missing the 'crv' member.");

        if (
            string.IsNullOrEmpty(curveName)
            || !EccCurveCatalog.TryGetByName(curveName, out var specification)
        )
            throw new ArgumentException($"The elliptic curve '{curveName}' is not supported.");

        var x = GetRequiredBase64UrlMember(root, JsonWebKeyParameterNames.X);
        var y = GetRequiredBase64UrlMember(root, JsonWebKeyParameterNames.Y);

        // The point is checked against the curve's domain before the platform sees it, because platforms disagree on
        // how they report an off-curve point; see ADR-1427.
        var fieldSizeBytes = specification.Domain.FieldSizeBytes;
        if (x.Length != fieldSizeBytes || y.Length != fieldSizeBytes)
            throw new ArgumentException(
                $"The supplied EC JWK's 'x' and 'y' members must each be {fieldSizeBytes} bytes for the curve '{curveName}'."
            );

        if (!specification.Domain.ContainsPoint(x, y))
            throw new ArgumentException(
                $"The supplied EC JWK's point is not on the curve '{curveName}'."
            );

        var parameters = new ECParameters
        {
            Curve = specification.Curve,
            Q = new ECPoint { X = x, Y = y },
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
    public SecretKey CreateRemoteKey(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        RemoteKeyBinding binding
    ) => SecretKeyFactory.CreateEccRemote(metadata, subjectPublicKeyInfo, binding);

    /// <inheritdoc />
    protected override JsonObject? WritePublicKeyJwk(AsymmetricSecretKey secretKey)
    {
        using var ecdsa = ((EccSecretKey)secretKey).ExportECDsa();
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        // The JWK 'crv' names the key's curve, matched by its OID, so two curves of one size are named apart; a key
        // whose curve the catalog does not hold has no name and is omitted (null) rather than guessed by its size.
        if (!EccCurveCatalog.TryGetByCurve(parameters.Curve, out var specification))
        {
            return null;
        }

        return new JsonObject
        {
            [JsonWebKeyParameterNames.KeyType] = JwkKeyType.Name,
            [JsonWebKeyParameterNames.Curve] = specification.CurveName,
            [JsonWebKeyParameterNames.X] = parameters.Q.X is { } x
                ? Base64Url.EncodeToString(x)
                : throw new InvalidOperationException("The ECC key is missing its X coordinate."),
            [JsonWebKeyParameterNames.Y] = parameters.Q.Y is { } y
                ? Base64Url.EncodeToString(y)
                : throw new InvalidOperationException("The ECC key is missing its Y coordinate."),
        };
    }

    private static string FormatCurves(IEnumerable<EccCurveSpecification> curves) =>
        string.Join(", ", curves.Select(curve => $"'{curve.CurveName}'"));

    // Resolves the requested curve by explicit name, else by key size, producing a human-readable error otherwise. A
    // curve fixes the size of its keys, so a named curve is refused for any other requested size, and two curves can
    // share a size, so a size selects a curve only when it is the one catalog curve of that size (ADR-1398).
    private bool TryResolveCurve(
        SecretKeyGenerationParameters parameters,
        [NotNullWhen(true)] out EccCurveSpecification? specification,
        out string? error
    )
    {
        if (parameters.Curve is { } curveName)
        {
            if (!EccCurveCatalog.TryGetByName(curveName, out var byName))
            {
                specification = null;
                error = $"The ECC curve '{curveName}' is not supported.";
                return false;
            }

            if (byName.CurveSizeBits != parameters.KeySizeBits)
            {
                specification = null;
                error =
                    $"The ECC key size '{parameters.KeySizeBits}' bits is invalid for the curve '{curveName}', "
                    + $"whose keys are {byName.CurveSizeBits} bits.";
                return false;
            }

            specification = byName;
            error = null;
            return true;
        }

        var curvesOfSize = EccCurveCatalog.GetByCurveSizeBits(parameters.KeySizeBits);
        if (curvesOfSize.Count == 1)
        {
            specification = curvesOfSize[0];
            error = null;
            return true;
        }

        specification = null;
        if (curvesOfSize.Count == 0)
        {
            var expected = string.Join(
                ", ",
                EccCurveCatalog.Curves.Select(curve =>
                    $"'{curve.CurveName}' ({curve.CurveSizeBits} bits)"
                )
            );
            error =
                $"The ECC key size '{parameters.KeySizeBits}' bits is invalid; expected {expected}.";
            return false;
        }

        error =
            $"The ECC key size '{parameters.KeySizeBits}' bits does not select a curve; "
            + FormatCurves(curvesOfSize)
            + " have that size, so name the curve.";
        return false;
    }
}
