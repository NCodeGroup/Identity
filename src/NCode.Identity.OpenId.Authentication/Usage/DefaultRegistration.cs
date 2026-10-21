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
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Usage;

internal static class DefaultRegistration
{
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddTenantUsageServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddOptions<TenantUsageOptions>();
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<DefaultTenantUsageRecorder>();
            serviceCollection.TryAddSingleton<ITenantUsageRecorder>(serviceProvider =>
                serviceProvider.GetRequiredService<DefaultTenantUsageRecorder>()
            );
            serviceCollection.AddHostedService<TenantUsageFlushService>();

            return builder;
        }
    }
}
