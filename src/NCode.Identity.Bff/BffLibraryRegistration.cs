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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Options;
using NCode.Registration;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to configure the backend-for-frontend (BFF) library.
/// </summary>
[PublicAPI]
public static class BffLibraryRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="BffLibrary"/>.</param>
    extension(IServiceBuilder<BffLibrary> builder)
    {
        /// <summary>
        /// Binds <see cref="BffOptions"/> strictly from the <c>Bff</c> section of <paramref name="configuration"/>, so a
        /// key that maps to no option fails when the options are first read.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the BFF settings.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<BffLibrary> AddBffConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(BffDefaults.ConfigurationSectionName);

            builder
                .ServiceCollection.AddOptions<BffOptions>()
                .Configure(options => section.BindStrictly(options));

            return builder;
        }
    }
}
