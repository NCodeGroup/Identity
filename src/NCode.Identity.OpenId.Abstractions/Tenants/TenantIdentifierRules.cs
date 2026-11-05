// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the rule for the form of a tenant identifier: 1 to 63 lowercase letters, digits or hyphens that starts and
/// ends with a letter or digit. A team's identifier follows the same form.
/// </summary>
// See ADR-1411
[PublicAPI]
public static partial class TenantIdentifierRules
{
    // An RFC 1123 host label in lowercase, so a name is equally valid as a URL path segment and as the leftmost label of
    // a hostname. '\z' rather than '$', which also matches before a trailing newline.
    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex TenantIdRegex();

    /// <summary>
    /// Determines whether the specified value is a valid tenant identifier.
    /// </summary>
    /// <param name="tenantId">The identifier to check.</param>
    /// <returns><see langword="true"/> when the value is a valid identifier; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidTenantId([NotNullWhen(true)] string? tenantId) =>
        !string.IsNullOrEmpty(tenantId) && TenantIdRegex().IsMatch(tenantId);
}
