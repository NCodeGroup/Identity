#region Copyright Preamble

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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Jose;
using NCode.Registration;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register the required services needed for using
/// Json Web Token (JWT) validation.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="JoseLibrary"/>.</param>
    extension(IServiceBuilder<JoseLibrary> builder)
    {
        /// <summary>
        /// Registers the required Json Web Token (JWT) services, layered on the <see cref="JoseLibrary"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<JsonWebTokensLibrary> AddJsonWebTokensLibrary()
        {
            var serviceCollection = builder.ServiceCollection;

            var newBuilder = builder.NewBuilder<JsonWebTokensLibrary>();

            serviceCollection.TryAddSingleton<IJsonWebTokenService, DefaultJsonWebTokenService>();

            return newBuilder;
        }
    }
}
