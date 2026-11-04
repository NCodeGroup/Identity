// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Jose.SdJwt.Presentation;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Jwt;
using NCode.Registration;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{TMarker}"/> to register Selective Disclosure for JWTs
/// (SD-JWT, RFC 9901): the issuer, the holder, the verifier, the <c>_sd_alg</c> hash algorithms and the
/// confirmation-key resolvers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> of the <see cref="JwtLibrary"/>, whose JWT validator
    /// verifies the issuer-signed JWT and the Key Binding JWT.</param>
    extension(IServiceBuilder<JwtLibrary> builder)
    {
        /// <summary>
        /// Registers the SD-JWT services with the default <see cref="SdJwtOptions"/>: <see cref="ISdJwtIssuer"/>,
        /// <see cref="ISdJwtHolder"/>, <see cref="ISdJwtVerifier"/>, the <c>sha-256</c>, <c>sha-384</c> and
        /// <c>sha-512</c> digest algorithms and the <c>jwk</c> confirmation-key resolver. Safe to call more than once.
        /// </summary>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<JwtLibrary> AddSdJwt() => builder.AddSdJwt(static _ => { });

        /// <summary>
        /// Registers the SD-JWT services: <see cref="ISdJwtIssuer"/>, <see cref="ISdJwtHolder"/>,
        /// <see cref="ISdJwtVerifier"/>, the <c>sha-256</c>, <c>sha-384</c> and <c>sha-512</c> digest algorithms and
        /// the <c>jwk</c> confirmation-key resolver, with the <see cref="SdJwtOptions"/> the callback configures,
        /// validated when the host starts. Safe to call more than once.
        /// </summary>
        /// <param name="configureOptions">The callback that configures the <see cref="SdJwtOptions"/>.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<JwtLibrary> AddSdJwt(Action<SdJwtOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<SdJwtOptions>()
                .Configure(configureOptions)
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<SdJwtOptions>, SdJwtOptionsValidator>()
            );

            // The digest algorithms are data, registered as instances beside any the application registers.
            serviceCollection.TryAddEnumerableInstance(SdJwtDigestAlgorithms.Sha256);
            serviceCollection.TryAddEnumerableInstance(SdJwtDigestAlgorithms.Sha384);
            serviceCollection.TryAddEnumerableInstance(SdJwtDigestAlgorithms.Sha512);
            serviceCollection.TryAddSingleton<
                ISdJwtDigestAlgorithmCatalog,
                DefaultSdJwtDigestAlgorithmCatalog
            >();

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<ISdJwtRandomSource, DefaultSdJwtRandomSource>();
            serviceCollection.TryAddSingleton<ISdJwtIssuer, DefaultSdJwtIssuer>();
            serviceCollection.TryAddSingleton<ISdJwtHolder, DefaultSdJwtHolder>();
            serviceCollection.TryAddSingleton<ISdJwtVerifier, DefaultSdJwtVerifier>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfirmationKeyResolver, JwkConfirmationKeyResolver>()
            );

            return builder;
        }
    }
}
