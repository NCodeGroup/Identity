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
using Microsoft.EntityFrameworkCore;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Provides a seam for satellite packages to contribute additional entities to the shared <see cref="OpenIdDbContext"/>
/// model, so their data participates in the same unit of work and transactions as the core entities. The context
/// constructor receives the registered contributors via dependency injection.
/// </summary>
/// <remarks>
/// Implementations must be registered as <b>stateless singletons</b>: the model is built once and cached, and the
/// context is pooled, so a contributor must hold no per-instance or per-request state and must configure the model
/// deterministically.
/// </remarks>
[PublicAPI]
public interface IOpenIdModelContributor
{
    /// <summary>
    /// Configures additional entities on the model being created for the <see cref="OpenIdDbContext"/>.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> being configured.</param>
    void Configure(ModelBuilder modelBuilder);
}
