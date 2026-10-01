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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;

/// <summary>
/// Converts an <see cref="RsaSecretKey"/> into an <see cref="RsaJsonWebKey"/> (<c>kty=RSA</c>),
/// publishing only the public members (<c>n</c> and <c>e</c>).
/// </summary>
internal class RsaJsonWebKeyConverter : JsonWebKeyConverter<RsaSecretKey>
{
    /// <inheritdoc />
    protected override JsonWebKey Convert(RsaSecretKey secretKey)
    {
        using var rsa = secretKey.ExportRSA();
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        var metadata = secretKey.Metadata;
        var modulus =
            parameters.Modulus
            ?? throw new InvalidOperationException("The RSA key is missing its modulus.");
        var exponent =
            parameters.Exponent
            ?? throw new InvalidOperationException("The RSA key is missing its exponent.");
        return new RsaJsonWebKey
        {
            KeyId = NullIfEmpty(metadata.KeyId),
            Use = NullIfEmpty(metadata.Use),
            Algorithm = NullIfEmpty(metadata.Algorithm),
            Modulus = Base64Url.Encode(modulus),
            Exponent = Base64Url.Encode(exponent),
        };
    }
}
