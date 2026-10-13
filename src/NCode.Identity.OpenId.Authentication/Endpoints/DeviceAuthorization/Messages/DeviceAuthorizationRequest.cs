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
