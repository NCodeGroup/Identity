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
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The context supplied to an <see cref="IAuthorizationRequestProfileRequirement"/> while validating an authorization
/// request.
/// </summary>
/// <param name="OpenIdContext">The ambient <see cref="OpenIdContext"/> (environment, error factory, tenant, services).</param>
/// <param name="Client">The resolved client the authorization request targets.</param>
/// <param name="Request">
/// The composed authorization request (request object, PAR and query parameters resolved), so a requirement can judge
/// the requested response type and response mode.
/// </param>
/// <param name="Delivery">
/// The delivery derived for the request: its redirect URI, the handler of its effective response mode, whose traits a
/// requirement may judge, and its state.
/// </param>
[PublicAPI]
public sealed record AuthorizationRequestProfileContext(
    OpenIdContext OpenIdContext,
    OpenIdClient Client,
    IAuthorizationRequest Request,
    AuthorizationResponseDelivery Delivery
);
