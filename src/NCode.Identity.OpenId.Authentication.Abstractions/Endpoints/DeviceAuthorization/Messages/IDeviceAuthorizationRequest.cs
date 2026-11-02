// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;

/// <summary>
/// Represents the message for an OAuth 2.0 device authorization request (RFC 8628 §3.1).
/// </summary>
[PublicAPI]
public interface IDeviceAuthorizationRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets or sets the scopes requested by the client.
    /// </summary>
    List<string>? Scopes { get; set; }
}
