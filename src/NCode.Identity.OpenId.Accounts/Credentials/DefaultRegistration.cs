#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Provides extension methods to register the default password hasher.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the default PBKDF2 <see cref="IPasswordHasher"/> implementation. A host may substitute another
        /// implementation (such as one backed by ASP.NET Core Identity) by registering its own before this call.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDefaultPasswordHasher()
        {
            serviceCollection.AddOptions();
            serviceCollection.TryAddSingleton<IPasswordHasher, DefaultPasswordHasher>();
            return serviceCollection;
        }
    }
}
