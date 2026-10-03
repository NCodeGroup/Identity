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

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register OpenID management services.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the specified <see cref="IAuthorizationHandler"/> implementation as a singleton.
        /// </summary>
        /// <typeparam name="THandler">The <see cref="IAuthorizationHandler"/> implementation type to register.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddAuthorizationHandler<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler
        >()
            where THandler : class, IAuthorizationHandler
        {
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IAuthorizationHandler, THandler>()
            );
            return serviceCollection;
        }
    }
}
