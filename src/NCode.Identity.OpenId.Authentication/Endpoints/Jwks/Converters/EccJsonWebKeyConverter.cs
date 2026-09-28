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

using JetBrains.Annotations;
using NCode.Encoders;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;

/// <summary>
/// Converts an <see cref="EccSecretKey"/> into an <see cref="EccJsonWebKey"/> (<c>kty=EC</c>),
/// publishing only the public members (<c>crv</c>, <c>x</c>, and <c>y</c>).
/// </summary>
[PublicAPI]
public class EccJsonWebKeyConverter(IEccCurveSpecificationRegistry curveSpecificationRegistry)
    : JsonWebKeyConverter<EccSecretKey>
{
    private IEccCurveSpecificationRegistry CurveSpecificationRegistry { get; } =
        curveSpecificationRegistry;

    /// <inheritdoc />
    protected override JsonWebKey? Convert(EccSecretKey secretKey)
    {
        // The set of supported curves (and their JWK 'crv' names) lives in the curve specification registry,
        // which is the single, discoverable source of truth. A key whose curve is not registered is omitted
        // rather than published with a guessed/invalid 'crv'.
        if (
            !CurveSpecificationRegistry.TryGetByCurveSizeBits(
                secretKey.KeySizeBits,
                out var curveSpecification
            )
        )
        {
            return null;
        }

        using var ecdsa = secretKey.ExportECDsa();
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        var metadata = secretKey.Metadata;
        return new EccJsonWebKey
        {
            KeyId = NullIfEmpty(metadata.KeyId),
            Use = NullIfEmpty(metadata.Use),
            Algorithm = NullIfEmpty(metadata.Algorithm),
            Curve = curveSpecification.CurveName,
            X = Base64Url.Encode(parameters.Q.X!),
            Y = Base64Url.Encode(parameters.Q.Y!),
        };
    }
}
