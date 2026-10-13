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
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;

/// <summary>
/// Represents a mediator command to load the effective CIBA backchannel authentication request. When the client sends
/// a signed (and optionally encrypted) <c>request</c> JWT, the default handler validates it and uses its claims as the
/// authoritative request parameters (OpenID Connect CIBA §7.1.1); otherwise the form-based request is used as-is.
/// </summary>
[PublicAPI]
public readonly record struct LoadBackchannelAuthenticationRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IBackchannelAuthenticationRequest Request
) : ICommand<IBackchannelAuthenticationRequest>;
