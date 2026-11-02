// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IDeviceAuthorizationRequest"/> abstraction.
/// </summary>
public sealed class DeviceAuthorizationRequest
    : OpenIdMessage<DeviceAuthorizationRequest>,
        IDeviceAuthorizationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceAuthorizationRequest"/> class.
    /// </summary>
    public DeviceAuthorizationRequest()
    {
        // nothing
    }

    private DeviceAuthorizationRequest(DeviceAuthorizationRequest other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public List<string>? Scopes
    {
        get => GetKnownParameter(OpenIdCommonParameters.Scopes);
        set => SetKnownParameter(OpenIdCommonParameters.Scopes, value);
    }

    /// <inheritdoc />
    public override DeviceAuthorizationRequest Clone() => new(this);
}
