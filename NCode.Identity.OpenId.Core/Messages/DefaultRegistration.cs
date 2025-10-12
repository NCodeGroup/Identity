#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Registration;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides extension methods to configure services and handlers for <see cref="OpenIdCoreLibrary"/>.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// Configures services and handlers for <see cref="OpenIdCoreLibrary"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
    public static IServiceBuilder<OpenIdCoreLibrary> AddMessageServices(
        this IServiceBuilder<OpenIdCoreLibrary> builder
    )
    {
        builder.AddMessageFactory<OpenIdError>();
        builder.AddMessageFactory<OpenIdMessage>();

        var serviceCollection = builder.ServiceCollection;

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICollectionDataSource<KnownParameter>,
            DefaultCommonParameterDataSource>());

        serviceCollection.TryAddSingleton<
            IKnownParameterCollectionProvider,
            DefaultKnownParameterCollectionProvider>();

        serviceCollection.TryAddSingleton<
            IOpenIdMessageFactorySelector,
            DefaultOpenIdMessageFactorySelector>();

        return builder;
    }
}
