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
/// Provides extension methods for <see cref="IServiceCollection"/> to register identity server services and handlers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// Registers identity server services and handlers into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    /// <param name="configure">The action to configure services for <see cref="IdentityLibrary"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection serviceCollection,
        Action<IServiceBuilder<IdentityLibrary>> configure
    )
    {
        var builder = ServiceBuilder.Register<IdentityLibrary>(serviceCollection);
        configure(builder);

        serviceCollection.AddFrameworkServices();
        serviceCollection.AddClaimServices();
        serviceCollection.AddEndpointServices();
        serviceCollection.AddLogicServices();
        serviceCollection.AddSettingServices();

        return serviceCollection;
    }

    private static void AddFrameworkServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.TryAddSingleton(ArrayPool<char>.Shared);
        serviceCollection.TryAddSingleton(ArrayPool<byte>.Shared);
    }

    private static void AddClaimServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<IClaimsService, DefaultClaimsService>();
        serviceCollection.TryAddSingleton<IClaimsSerializer>(DefaultClaimsSerializer.Singleton);
    }

    private static void AddEndpointServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<IIdentityEndpointRouteBuilder, DefaultIdentityEndpointRouteBuilder>();
    }

    private static void AddLogicServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<ICryptoService, DefaultCryptoService>();
    }

    private static void AddSettingServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<ISettingDescriptorCollectionProvider, DefaultSettingDescriptorCollectionProvider>();
        serviceCollection.TryAddSingleton<IReadOnlySettingCollectionProviderFactory, DefaultReadOnlySettingCollectionProviderFactory>();
        serviceCollection.TryAddSingleton<ISettingDescriptorJsonProvider, DefaultSettingDescriptorJsonProvider>();
        serviceCollection.TryAddSingleton<ISettingCollectionFactory, DefaultSettingCollectionFactory>();
        serviceCollection.TryAddSingleton<ISettingSerializer, DefaultSettingSerializer>();
    }
}
