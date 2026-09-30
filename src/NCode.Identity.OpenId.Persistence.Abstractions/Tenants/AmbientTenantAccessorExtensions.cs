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

namespace NCode.Identity.OpenId.Persistence.Tenants;

/// <summary>
/// Provides extension methods for the <see cref="IAmbientTenantAccessor"/> abstraction.
/// </summary>
[PublicAPI]
public static class AmbientTenantAccessorExtensions
{
    extension(IAmbientTenantAccessor accessor)
    {
        /// <summary>
        /// Gets the identifier of the ambient tenant for the current request, throwing when no tenant scope is
        /// active. Use this from a tenant-bound operation that requires the scope to have been established — for
        /// example, deriving the owning tenant of a resource being created.
        /// </summary>
        /// <returns>The identifier of the ambient tenant.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no ambient tenant scope is active.</exception>
        public string GetRequiredTenantId()
        {
            ArgumentNullException.ThrowIfNull(accessor);

            return accessor.TenantId
                ?? throw new InvalidOperationException(
                    "The ambient tenant scope was not established for the request."
                );
        }
    }
}
