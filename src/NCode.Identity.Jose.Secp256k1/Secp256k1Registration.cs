// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Secp256k1.Algorithms;
using NCode.Identity.Jose.Secp256k1.KeyTypes;
using NCode.Identity.Registration;

namespace NCode.Identity.Jose.Secp256k1;

/// <summary>
/// Registers the secp256k1 curve and the <c>ES256K</c> algorithm when the platform supports the curve.
/// </summary>
// See ADR-1392.
internal static class Secp256k1Registration
{
    /// <summary>
    /// Registers the secp256k1 curve and the <c>ES256K</c> algorithm once, or nothing when the platform does not support
    /// the curve, so that no key, <c>JWK</c> or algorithm is offered that the host cannot use.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register into.</param>
    /// <param name="isPlatformSupported">Whether the platform's cryptography supports secp256k1.</param>
    /// <returns>The same <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection Register(
        IServiceCollection serviceCollection,
        bool isPlatformSupported
    )
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        if (!isPlatformSupported)
        {
            return serviceCollection;
        }

        // Each is registered as an instance, kept beside the other curves and ECDSA algorithms the application
        // registers.
        serviceCollection.TryAddEnumerableInstance(Secp256k1Curve.Specification);
        serviceCollection.TryAddEnumerableInstance(Secp256k1Algorithms.Es256K);

        return serviceCollection;
    }
}
