// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
