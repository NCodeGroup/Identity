// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Contains constants for the approval status of a pending device authorization request (RFC 8628).
/// </summary>
[PublicAPI]
public static class DeviceAuthorizationStatus
{
    /// <summary>
    /// Indicates the end user has not yet approved or denied the device authorization request.
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    /// Indicates the end user has approved the device authorization request.
    /// </summary>
    public const string Approved = "approved";

    /// <summary>
    /// Indicates the end user has denied the device authorization request.
    /// </summary>
    public const string Denied = "denied";
}
