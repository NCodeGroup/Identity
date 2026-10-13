#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;

/// <summary>
/// Represents a mediator command that builds the OpenID Provider discovery metadata document served at
/// <c>/.well-known/openid-configuration</c>. Each registered handler contributes entries to the shared
/// <paramref name="Metadata"/> dictionary, so a capability is advertised by registering a handler rather than by
/// changing the endpoint.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="Metadata">The mutable metadata document each handler contributes discovery entries to.</param>
[PublicAPI]
public readonly record struct DiscoverMetadataCommand(
    OpenIdContext OpenIdContext,
    IDictionary<string, object> Metadata
) : ICommand;
