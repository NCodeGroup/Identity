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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Jose;
using NCode.Jose.Extensions;
using NCode.SystemClock;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides extension methods for adding Json Web Token (JWT) services to the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Json Web Token (JWT) services to the specified &lt;see cref="IServiceCollection"/&gt; instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJsonWebTokenService(this IServiceCollection services) =>
        AddJsonWebTokenService(services, _ => { });

    /// <summary>
    /// Adds Json Web Token (JWT) services to the specified <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">The action used to configure the <see cref="JoseSerializerOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJsonWebTokenService(this IServiceCollection services, Action<JoseSerializerOptions> configureOptions)
    {
        services.AddJose(configureOptions);

        services.TryAddSingleton<ISystemClockSecondsAccuracy, SystemClockSecondsAccuracy>();
        services.TryAddSingleton<IJsonWebTokenService, JsonWebTokenService>();

        return services;
    }
}
