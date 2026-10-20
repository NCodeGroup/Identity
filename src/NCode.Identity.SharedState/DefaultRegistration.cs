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
using NCode.Registration;

namespace NCode.Identity.SharedState;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the shared-state library.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Mints the <see cref="SharedStateLibrary"/> marker that the shared-state adapters require. It registers no
        /// adapter by itself; call <c>AddInProcessExpiringKeySet</c> for the built-in single-replica adapter, or
        /// register a shared adapter (for example <c>AddValkey</c>) for a deployment that runs several replicas.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> for the shared-state library so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SharedStateLibrary> AddSharedStateServices()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            return serviceCollection.NewBuilder<SharedStateLibrary>();
        }
    }
}
