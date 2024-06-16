#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Registration;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required core services for OpenId.
/// </summary>
[PublicAPI]
public static class OpenIdCoreRegistration
{
    /// <summary>
    /// Registers the required core services for OpenId into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddOpenIdCoreServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton(TimeProvider.System);

        serviceCollection.TryAddSingleton<ICryptoService, DefaultCryptoService>();
        serviceCollection.TryAddSingleton<IPersistedGrantService, DefaultPersistedGrantService>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICollectionDataSource<KnownParameter>, DefaultKnownParameterDataSource>());

        serviceCollection.TryAddSingleton<
            IKnownParameterCollectionProvider,
            DefaultKnownParameterCollectionProvider>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICollectionDataSource<SettingDescriptor>, DefaultSettingDescriptorDataSource>());

        serviceCollection.TryAddSingleton<
            ISettingDescriptorCollectionProvider,
            DefaultSettingDescriptorCollectionProvider>();

        serviceCollection.TryAddSingleton<
            ISettingDescriptorJsonProvider,
            DefaultSettingDescriptorJsonProvider>();

        serviceCollection.TryAddSingleton<
            ISettingSerializer,
            DefaultSettingSerializer>();

        serviceCollection.AddSingleton<DefaultOpenIdServer>();
        serviceCollection.TryAddSingleton<OpenIdServer>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultOpenIdServer>());
        serviceCollection.TryAddSingleton<IOpenIdErrorFactory>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultOpenIdServer>());

        return serviceCollection;
    }
}
