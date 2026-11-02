// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;

/// <summary>
/// Represents a mediator command to create and persist the <c>device_code</c> and <c>user_code</c> grants for a device
/// authorization request, and produce the <see cref="DeviceAuthorizationResult"/> (RFC 8628 §3.2).
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the device authorization request.</param>
/// <param name="Request">The validated <see cref="IDeviceAuthorizationRequest"/> the grants are created for.</param>
[PublicAPI]
public readonly record struct CreateDeviceAuthorizationRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IDeviceAuthorizationRequest Request
) : ICommand<DeviceAuthorizationResult>;
