// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Secp256k1.KeyTypes;
using NCode.Identity.Registration;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Secp256k1;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{TMarker}"/> to register secp256k1 support: the
/// secp256k1 curve (<c>JWK</c> <c>crv</c> <c>secp256k1</c>) and the <c>ES256K</c> <c>JWS</c> signature algorithm
/// (RFC 8812).
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> of the <see cref="JoseLibrary"/>, whose curve catalog
    /// and algorithms the registration adds to.</param>
    extension(IServiceBuilder<JoseLibrary> builder)
    {
        /// <summary>
        /// Registers secp256k1 support where the platform's cryptography supports the curve, and nothing where it does
        /// not, such as on macOS: the secp256k1 <see cref="EccCurveSpecification"/>, through which <c>ecc</c> keys on
        /// the curve are generated, imported and published as <c>JWK</c>s with <c>crv</c> <c>secp256k1</c>, and the
        /// <c>ES256K</c> <see cref="EccSignatureAlgorithmSpecification"/> (<c>ECDSA</c> over secp256k1 with SHA-256,
        /// RFC 8812), which accepts only keys on secp256k1. Safe to call more than once.
        /// </summary>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<JoseLibrary> AddSecp256k1()
        {
            Secp256k1Registration.Register(builder.ServiceCollection, Secp256k1Curve.IsSupported);
            return builder;
        }
    }
}
