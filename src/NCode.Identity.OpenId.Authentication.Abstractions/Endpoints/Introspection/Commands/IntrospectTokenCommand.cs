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
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;

/// <summary>
/// Represents a mediator command to populate a token introspection <see cref="Result"/> for an authenticated caller
/// (RFC 7662). Handlers determine whether the token is active and contribute its claims; register additional handlers
/// to enrich the response.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> on whose behalf introspection is performed.</param>
/// <param name="TokenIntrospectionRequest">The <see cref="ITokenIntrospectionRequest"/> identifying the token to introspect.</param>
/// <param name="Result">The mutable <see cref="IntrospectionResult"/> that handlers populate with the token's active state and claims.</param>
[PublicAPI]
public readonly record struct IntrospectTokenCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenIntrospectionRequest TokenIntrospectionRequest,
    IntrospectionResult Result
) : ICommand;
