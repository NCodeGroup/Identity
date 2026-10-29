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

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Memoizes the effective environment type of each tenant a management request names, in the request's
/// <see cref="HttpContext.Items"/>, so every audit event of the request reads the store at most once per tenant, and a
/// handler that deletes a tenant can record the type the tenant had before its row is gone.
/// </summary>
internal static class ManagementTenantEnvironment
{
    private const string KeyPrefix = "NCode.Identity.OpenId.Management.TenantEnvironmentType:";

    /// <summary>
    /// Remembers a tenant's effective environment type for the rest of the request.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="environmentType">The tenant's effective environment type.</param>
    public static void Remember(HttpContext httpContext, string tenantId, string environmentType)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Items[KeyPrefix + tenantId] = environmentType;
    }

    /// <summary>
    /// Gets a tenant's remembered environment type.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="environmentType">When this method returns, contains the remembered type, if any.</param>
    /// <returns><see langword="true"/> when a type is remembered; otherwise, <see langword="false"/>.</returns>
    public static bool TryRecall(
        HttpContext httpContext,
        string tenantId,
        [NotNullWhen(true)] out string? environmentType
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var found = httpContext.Items.TryGetValue(KeyPrefix + tenantId, out var value);
        environmentType = found ? value as string : null;
        return environmentType is not null;
    }
}
