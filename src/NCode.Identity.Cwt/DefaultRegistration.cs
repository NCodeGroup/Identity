// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Cose;
using NCode.Identity.Cwt.Issuance;
using NCode.Identity.Cwt.Validation;
using NCode.Identity.Registration;

namespace NCode.Identity.Cwt;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{TMarker}"/> to register CBOR Web Tokens (CWT, RFC 8392):
/// the issuer and the validator.
/// </summary>
// See ADR-1410.
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> of the <see cref="CoseLibrary"/>, whose messages
    /// carry the tokens.</param>
    extension(IServiceBuilder<CoseLibrary> builder)
    {
        /// <summary>
        /// Registers the CWT services: <see cref="ICwtIssuer"/> and <see cref="ICwtValidator"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> of the <see cref="CwtLibrary"/>.</returns>
        public IServiceBuilder<CwtLibrary> AddCwtLibrary()
        {
            var serviceCollection = builder.ServiceCollection;
            var newBuilder = builder.NewBuilder<CwtLibrary>();

            serviceCollection.TryAddSingleton<ICwtIssuer, DefaultCwtIssuer>();
            serviceCollection.TryAddSingleton<ICwtValidator, DefaultCwtValidator>();
            serviceCollection.TryAddSingleton(TimeProvider.System);

            return newBuilder;
        }
    }
}
