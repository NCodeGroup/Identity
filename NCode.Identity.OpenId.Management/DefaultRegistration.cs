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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Endpoints.Servers;
using NCode.Registration;

namespace NCode.Identity.OpenId.Management;

[PublicAPI]
public static class DefaultRegistration
{
    public static IServiceBuilder AddOpenIdManagement(this IServiceBuilder serviceBuilder)
    {
        var services = serviceBuilder.ServiceCollection;

        services.AddAuthorization();
        services.AddAuthorizationHandler<GlobalAdminHandler>();
        services.AddAuthorizationHandler<TenantAdminHandler>();

        serviceBuilder.AddEndpointProvider<ServerApiEndpointHandler>();

        return serviceBuilder;
    }

    public static IServiceCollection AddAuthorizationHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where THandler : class, IAuthorizationHandler
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, THandler>());
        return services;
    }
}
