﻿#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Tenants;
using NIdentity.OpenId.Tenants.Providers;

namespace NIdentity.OpenId.Registration;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register core tenant services and handlers
/// </summary>
public static class CoreTenantRegistration
{
    /// <summary>
    /// Registers core tenant services and handlers into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddCoreTenantServices(this IServiceCollection services)
    {
        services.AddSingleton<IOpenIdTenantCache, DefaultOpenIdTenantCache>();
        services.AddSingleton<IOpenIdTenantFactory, DefaultOpenIdTenantFactory>();
        services.AddSingleton<IOpenIdTenantProviderSelector, DefaultOpenIdTenantProviderSelector>();

        services.AddSingleton<IOpenIdTenantProvider, DefaultStaticSingleOpenIdTenantProvider>();
        services.AddSingleton<IOpenIdTenantProvider, DefaultDynamicByHostOpenIdTenantProvider>();
        services.AddSingleton<IOpenIdTenantProvider, DefaultDynamicByPathOpenIdTenantProvider>();

        return services;
    }
}
