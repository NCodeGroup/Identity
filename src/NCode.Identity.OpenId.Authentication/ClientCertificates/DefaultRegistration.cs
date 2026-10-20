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
        /// Configures the client-certificate resolver, the chain validator, and the decoders of the known
        /// forwarded-certificate formats. The forwarded-header settings are configured by the host; the server's
        /// <c>AddConfiguration</c> binds them from <c>OpenId:MutualTls</c>.
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

            serviceCollection.TryAddSingleton<
                IClientCertificateResolver,
                DefaultClientCertificateResolver
            >();

            serviceCollection.TryAddSingleton<
                IClientCertificateChainValidator,
                DefaultClientCertificateChainValidator
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
