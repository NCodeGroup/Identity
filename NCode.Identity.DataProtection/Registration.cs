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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Identity.DataProtection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the required services needed for data protection.
/// </summary>
[PublicAPI]
public static class Registration
{
    /// <summary>
    /// Registers the required services needed for data protection into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IDataProtectionBuilder"/> instance for configuring data protection.</returns>
    public static IDataProtectionBuilder AddDataProtectionServices(
        this IServiceCollection serviceCollection)
    {
        return serviceCollection.AddDataProtectionServices(_ => { });
    }

    /// <summary>
    /// Registers the required services needed for data protection into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="setupAction">An <see cref="Action{DataProtectionOptions}"/> to configure the provided <see cref="DataProtectionOptions"/>.</param>
    /// <returns>The <see cref="IDataProtectionBuilder"/> instance for configuring data protection.</returns>
    public static IDataProtectionBuilder AddDataProtectionServices(
        this IServiceCollection serviceCollection,
        Action<DataProtectionOptions> setupAction)
    {
        serviceCollection.TryAddSingleton<ISecureDataProtectionProvider, DefaultSecureDataProtectionProvider>();

        return serviceCollection.AddDataProtection(setupAction);
    }
}
