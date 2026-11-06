// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides extension methods for reading BFF state off a <see cref="ClaimsPrincipal"/>.
/// </summary>
[PublicAPI]
public static class BffClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal principal)
    {
        /// <summary>
        /// Gets the session that the connection of the principal holds.
        /// </summary>
        /// <returns>
        /// The <see cref="BffConnectionIdentity.Session"/> of the first <see cref="BffConnectionIdentity"/> of the
        /// principal, or <see langword="null"/> when the principal has none.
        /// </returns>
        public IBffConnectionSession? GetBffConnectionSession()
        {
            ArgumentNullException.ThrowIfNull(principal);

            return principal.Identities.OfType<BffConnectionIdentity>().FirstOrDefault()?.Session;
        }
    }
}
