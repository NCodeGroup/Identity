// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides the default implementation of <see cref="IManagementCallerFeature"/>.
/// </summary>
internal sealed class DefaultManagementCallerFeature(ClaimsPrincipal caller)
    : IManagementCallerFeature
{
    /// <inheritdoc />
    public ClaimsPrincipal Caller { get; } = caller;
}
