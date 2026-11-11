// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Claims;
using NCode.Identity.Cryptography;
using NCode.Identity.Registration;
using NCode.Identity.Registration.AspNetCore;
using NCode.Identity.Settings;

namespace NCode.Identity;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register identity services and handlers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers identity services and handlers into the provided <see cref="IServiceCollection"/> instance.
        /// </summary>
        [PublicAPI]
        public IServiceBuilder<IdentityLibrary> AddIdentityLibrary()
        {
            // Framework
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton(ArrayPool<char>.Shared);
            serviceCollection.TryAddSingleton(ArrayPool<byte>.Shared);

            // Claims
            serviceCollection.TryAddSingleton<IClaimsService, DefaultClaimsService>();
            serviceCollection.TryAddSingleton<IClaimsSerializer>(DefaultClaimsSerializer.Singleton);

            // Endpoints
            serviceCollection.AddEndpointGroupRouting();

            // Cryptography
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

            // Settings
            serviceCollection.AddSettingsLibrary();

            return serviceCollection.NewBuilder<IdentityLibrary>();
        }
    }
}
