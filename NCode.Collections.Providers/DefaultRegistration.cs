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

namespace NCode.Collections.Providers;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the required services for collection providers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// Registers the required services for collection providers into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddCollectionProviders(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<
            INullChangeToken,
            NullChangeToken>();

        serviceCollection.TryAddSingleton<
            ICollectionDataSourceFactory,
            DefaultCollectionDataSourceFactory>();

        serviceCollection.TryAddSingleton<
            ICollectionProviderFactory,
            DefaultCollectionProviderFactory>();

        return serviceCollection;
    }
}
