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
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.SharedState.InProcess;
using NCode.Registration;

namespace NCode.Identity.SharedState;

/// <summary>
/// Provides opt-in extension methods for the <see cref="SharedStateLibrary"/> builder, so a shared-state adapter cannot
/// be added without first calling <c>AddSharedStateServices</c>.
/// </summary>
[PublicAPI]
public static class SharedStateLibraryRegistration
{
    /// <param name="builder">The shared-state-library builder returned by <c>AddSharedStateServices</c>.</param>
    extension(IServiceBuilder<SharedStateLibrary> builder)
    {
        /// <summary>
        /// Registers the built-in in-process <see cref="IExpiringKeySet"/> adapter. Its keys live in the memory of one
        /// process, so it is correct only for a deployment that runs a single replica: a key recorded by one replica is
        /// not seen by another, and every key is lost when the process restarts. The registration is a default
        /// (<c>TryAdd</c>), so a deployment that registered its own <see cref="IExpiringKeySet"/> first keeps that
        /// adapter.
        /// </summary>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SharedStateLibrary> AddInProcessExpiringKeySet()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<IExpiringKeySet, DefaultInProcessExpiringKeySet>();

            return builder;
        }
    }
}
