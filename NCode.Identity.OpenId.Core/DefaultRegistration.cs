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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Serialization;
using NCode.Registration;

namespace NCode.Identity.OpenId;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder"/> to configure OpenId Core services and handlers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="IdentityLibrary"/>.</param>
    extension(IServiceBuilder<IdentityLibrary> builder)
    {
        /// <summary>
        /// Configures OpenId Core services and handlers.
        /// </summary>
        public IServiceBuilder<IdentityLibrary> AddOpenIdCore()
        {
            return builder.AddOpenIdCore(_ => { });
        }

        /// <summary>
        /// Configures OpenId Core services and handlers.
        /// </summary>
        /// <param name="configure">The action to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
        public IServiceBuilder<IdentityLibrary> AddOpenIdCore(Action<IServiceBuilder<OpenIdCoreLibrary>> configure)
        {
            var newBuilder = builder.Register<OpenIdCoreLibrary>();
            configure(newBuilder);

            newBuilder
                .AddSerializationServices()
                .AddEnvironmentServices()
                .AddExceptionServices()
                .AddMessageServices()
                .AddResultServices();

            return builder;
        }
    }
}
