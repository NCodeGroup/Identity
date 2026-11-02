// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides extension methods to configure the services that resolve the mutual-TLS client certificate a client
/// presented.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the client-certificate resolver, the chain validator, the revocation checker with its sources
        /// and their guarded HTTP client, and the decoders of the known forwarded-certificate formats. The
        /// forwarded-header settings are configured by the host; the server's <c>AddConfiguration</c> binds them from
        /// <c>OpenId:MutualTls</c>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddClientCertificateServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddOptions<MutualTlsForwardingOptions>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<MutualTlsForwardingOptions>,
                    MutualTlsForwardingOptionsValidator
                >()
            );

            serviceCollection.AddOptions<ClientCertificateRevocationOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<ClientCertificateRevocationOptions>,
                    ClientCertificateRevocationOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton<
                IClientCertificateResolver,
                DefaultClientCertificateResolver
            >();

            serviceCollection.TryAddSingleton<
                IClientCertificateChainValidator,
                DefaultClientCertificateChainValidator
            >();

            serviceCollection.TryAddSingleton<RevocationDataFetcher>();
            serviceCollection.TryAddSingleton<RevocationRetrievalCache>();

            // The CRL source is registered as itself because the OCSP source checks delegated responders through it.
            serviceCollection.TryAddSingleton<CrlClientCertificateRevocationSource>();

            // Sources are asked in registration order: OCSP, then the CRLs. See ADR-1259.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCertificateRevocationSource,
                    OcspClientCertificateRevocationSource
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCertificateRevocationSource,
                    CrlClientCertificateRevocationSource
                >(serviceProvider =>
                    serviceProvider.GetRequiredService<CrlClientCertificateRevocationSource>()
                )
            );

            serviceCollection.TryAddSingleton<
                IClientCertificateRevocationChecker,
                DefaultClientCertificateRevocationChecker
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCertificateHeaderDecoder,
                    Base64DerClientCertificateHeaderDecoder
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCertificateHeaderDecoder,
                    PemClientCertificateHeaderDecoder
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCertificateHeaderDecoder,
                    UrlEncodedPemClientCertificateHeaderDecoder
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IClientCertificateHeaderDecoder,
                    EnvoyXfccClientCertificateHeaderDecoder
                >()
            );

            return builder;
        }
    }
}
