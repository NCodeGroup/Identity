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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;

/// <summary>
/// Represents a mediator command that validates an OAuth 2.0 device authorization request (RFC 8628) against the
/// protocol rules and the client's configuration. A handler reports a validation failure as an OpenID error; a
/// successful validation completes without a result.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the device authorization request.</param>
/// <param name="Request">The <see cref="IDeviceAuthorizationRequest"/> being validated.</param>
[PublicAPI]
public readonly record struct ValidateDeviceAuthorizationRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IDeviceAuthorizationRequest Request
) : ICommand;
