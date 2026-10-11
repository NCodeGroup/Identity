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

using NCode.Encoders;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;

/// <summary>
/// Converts an <see cref="EccSecretKey"/> into an <see cref="EccJsonWebKey"/> (<c>kty=EC</c>),
/// publishing only the public members (<c>crv</c>, <c>x</c>, and <c>y</c>).
/// </summary>
internal class EccJsonWebKeyConverter(IEccCurveCatalog eccCurveCatalog)
    : JsonWebKeyConverter<EccSecretKey>
{
    private IEccCurveCatalog EccCurveCatalog { get; } = eccCurveCatalog;

    /// <inheritdoc />
    protected override JsonWebKey? Convert(EccSecretKey secretKey)
    {
        using var ecdsa = secretKey.ExportECDsa();
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        // The supported curves (and their JWK 'crv' names) live in the shared curve catalog (ADR-0068). Resolve by
        // the key's actual curve so two same-size curves are named correctly, falling back to the curve size. A
        // key whose curve is not in the catalog is omitted rather than published with a guessed/invalid 'crv'.
        if (
            !EccCurveCatalog.TryGetByCurve(parameters.Curve, out var curveSpecification)
            && !EccCurveCatalog.TryGetByCurveSizeBits(secretKey.KeySizeBits, out curveSpecification)
        )
        {
            return null;
        }

        var metadata = secretKey.Metadata;
        var x =
            parameters.Q.X
            ?? throw new InvalidOperationException("The ECC key is missing its X coordinate.");
        var y =
            parameters.Q.Y
            ?? throw new InvalidOperationException("The ECC key is missing its Y coordinate.");
        return new EccJsonWebKey
        {
            KeyId = NullIfEmpty(metadata.KeyId),
            Use = NullIfEmpty(metadata.Use),
            Algorithm = NullIfEmpty(metadata.Algorithm),
            Curve = curveSpecification.CurveName,
            X = Base64Url.Encode(x),
            Y = Base64Url.Encode(y),
        };
    }
}
