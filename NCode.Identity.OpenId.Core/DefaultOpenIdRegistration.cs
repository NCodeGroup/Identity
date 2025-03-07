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

using System.Buffers;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Claims;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Subject;
using NCode.Identity.Secrets;

namespace NCode.Identity.OpenId;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the required services and handlers for OpenId.
/// </summary>
[PublicAPI]
public static class DefaultOpenIdRegistration
{
    /// <summary>
    /// Gets or sets the default low Mediator priority for all OpenId handlers and processors
    /// The default value is <c>-100</c> so that handlers provided from the application can be registered with a higher priority.
    /// </summary>
    public static int MediatorPriorityLow { get; set; } = -100;

    /// <summary>
    /// Gets or sets the default high Mediator priority for all OpenId handlers and processors
    /// The default value is <c>100</c> so that handlers provided from this library can be registered with a higher priority.
    /// </summary>
    public static int MediatorPriorityHigh { get; set; } = 100;

    /// <summary>
    /// Registers the required services and handlers for OpenId into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddOpenIdServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.VerifySecretServicesAreRegistered();
        serviceCollection.VerifyJoseServicesAreRegistered();

        // random services

        serviceCollection.TryAddSingleton(
            TimeProvider.System);

        serviceCollection.TryAddSingleton(
            ArrayPool<char>.Shared);

        serviceCollection.TryAddSingleton(
            ArrayPool<byte>.Shared);

        serviceCollection.TryAddSingleton<
            ICryptoService,
            DefaultCryptoService>();

        serviceCollection.TryAddSingleton<
            IPersistedGrantService,
            DefaultPersistedGrantService>();

        serviceCollection.TryAddSingleton<
            IClaimsService,
            DefaultClaimsService>();

        serviceCollection.TryAddSingleton<IClaimsSerializer>(
            DefaultClaimsSerializer.Singleton);

        // settings

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICollectionDataSource<SettingDescriptor>,
            DefaultSettingDescriptorDataSource>());

        serviceCollection.TryAddSingleton<
            ISettingDescriptorCollectionProvider,
            DefaultSettingDescriptorCollectionProvider>();

        serviceCollection.TryAddSingleton<
            IReadOnlySettingCollectionProviderFactory,
            DefaultReadOnlySettingCollectionProviderFactory>();

        serviceCollection.TryAddSingleton<
            ISettingDescriptorJsonProvider,
            DefaultSettingDescriptorJsonProvider>();

        serviceCollection.TryAddSingleton<
            ISettingSerializer,
            DefaultSettingSerializer>();

        // server

        serviceCollection.TryAddSingleton<
            IOpenIdServerFactory,
            DefaultOpenIdServerFactory>();

        serviceCollection.TryAddSingleton<
            IOpenIdServerProvider,
            DefaultOpenIdServerProvider>();

        // subject

        serviceCollection.TryAddSingleton<
            ICommandHandler<ValidateSubjectCommand>,
            DefaultValidateSubjectHandler>();

        // environment

        serviceCollection.TryAddSingleton<
            IOpenIdEnvironmentFactory,
            DefaultOpenIdEnvironmentFactory>();

        serviceCollection.TryAddSingleton<
            IOpenIdEnvironmentProvider,
            DefaultOpenIdEnvironmentProvider>();

        serviceCollection.TryAddSingleton<
            IOpenIdExceptionHandler,
            DefaultOpenIdExceptionHandler>();

        return serviceCollection;
    }
}
