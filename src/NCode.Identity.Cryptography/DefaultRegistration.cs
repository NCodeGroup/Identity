// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the cryptography services.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the default cryptography service and its binary encoders, minting the
        /// <see cref="CryptographyLibrary"/> marker that the libraries built on cryptography require.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> for the cryptography library so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<CryptographyLibrary> AddCryptographyLibrary()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            var newBuilder = serviceCollection.NewBuilder<CryptographyLibrary>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBinaryEncoder, Base64UrlBinaryEncoder>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBinaryEncoder, Base64BinaryEncoder>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IBinaryEncoder, HexBinaryEncoder>()
            );
            serviceCollection.TryAddSingleton<ICryptoService, DefaultCryptoService>();

            return newBuilder;
        }
    }
}
