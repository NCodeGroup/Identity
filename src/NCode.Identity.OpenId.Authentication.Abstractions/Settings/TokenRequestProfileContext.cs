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
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The context supplied to an <see cref="ITokenRequestProfileRequirement"/> while evaluating a token request.
/// </summary>
/// <param name="OpenIdContext">The ambient <see cref="OpenIdContext"/> (environment, error factory, tenant, services).</param>
/// <param name="Client">The authenticated client making the token request.</param>
/// <param name="Confirmation">The sender-constraining confirmation computed for the request (DPoP / mutual-TLS), if any.</param>
[PublicAPI]
public sealed record TokenRequestProfileContext(
    OpenIdContext OpenIdContext,
    OpenIdClient Client,
    TokenConfirmation Confirmation
);
