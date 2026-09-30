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

namespace NCode.Identity.OpenId.ResourceServers;

/// <summary>
/// Seeds the reserved, system-owned resource servers (contributed by <see cref="ISystemResourceServerProvider"/>)
/// into a tenant so that the tenant is self-contained.
/// </summary>
[PublicAPI]
public interface ISystemResourceServerSeeder
{
    /// <summary>
    /// Ensures the reserved system resource servers exist for the specified tenant, creating any that are missing.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to seed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SeedAsync(string tenantId, CancellationToken cancellationToken);
}
