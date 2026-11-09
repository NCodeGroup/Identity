// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides the caller that the management authentication filter authenticated from the request's management access
/// token. The feature is present only for such a caller.
/// </summary>
[PublicAPI]
public interface IManagementCallerFeature
{
    /// <summary>
    /// Gets the principal that the request's management access token authenticates.
    /// </summary>
    ClaimsPrincipal Caller { get; }
}
