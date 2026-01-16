#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Buffers;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Claims;
using NCode.Identity.Endpoints;
using NCode.Identity.Logic;
using NCode.Identity.Settings;
using NCode.Registration;

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
            serviceCollection.TryAddSingleton<IIdentityEndpointRouteBuilder, DefaultIdentityEndpointRouteBuilder>();

            // Logic
            serviceCollection.TryAddSingleton<ICryptoService, DefaultCryptoService>();

            // Settings
            serviceCollection.TryAddSingleton<ISettingDescriptorCollectionProvider, DefaultSettingDescriptorCollectionProvider>();
            serviceCollection.TryAddSingleton<IReadOnlySettingCollectionProviderFactory, DefaultReadOnlySettingCollectionProviderFactory>();
            serviceCollection.TryAddSingleton<ISettingDescriptorJsonProvider, DefaultSettingDescriptorJsonProvider>();
            serviceCollection.TryAddSingleton<ISettingCollectionFactory, DefaultSettingCollectionFactory>();
            serviceCollection.TryAddSingleton<ISettingSerializer, DefaultSettingSerializer>();

            return serviceCollection.NewBuilder<IdentityLibrary>();
        }
    }
}
