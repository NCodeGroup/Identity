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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the required services needed for using
/// Json Web Token (JWT) validation.
/// </summary>
[PublicAPI]
public static class Registration
{
    /// <summary>
    /// Registers the required Json Web Token (JWT) services to the specified <see cref="IServiceCollection"/> instance.
    /// Make sure to also register the required services from the <c>NCode.Identity.Jose</c> package.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJsonWebTokenServices(
        this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<IJsonWebTokenService, DefaultJsonWebTokenService>();

        return serviceCollection;
    }
}
