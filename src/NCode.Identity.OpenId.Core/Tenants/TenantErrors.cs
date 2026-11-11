// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.AspNetCore;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the errors a tenant's resolution and provisioning answer.
/// </summary>
internal static class TenantErrors
{
    /// <summary>
    /// Creates the <c>404</c> answer for a tenant that does not exist.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that could not be found.</param>
    internal static HttpResultException NotFound(string tenantId) =>
        TypedResults
            .NotFound()
            .AsException($"The tenant with identifier '{tenantId}' could not be found.");
}
