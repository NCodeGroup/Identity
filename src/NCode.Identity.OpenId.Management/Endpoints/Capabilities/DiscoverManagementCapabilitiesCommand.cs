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
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// A mediator command that composes the management capabilities discovery document. Each registered
/// <see cref="ICommandHandler{TCommand}"/> contributes a named capability section to <see cref="Capabilities"/>,
/// so new capabilities are exposed by registering a handler rather than by changing the endpoint.
/// </summary>
/// <param name="OpenIdContext">The ambient OpenID context for the request.</param>
/// <param name="Capabilities">The mutable capability document each provider contributes a section to.</param>
// See ADR-0067.
[PublicAPI]
public readonly record struct DiscoverManagementCapabilitiesCommand(
    OpenIdContext OpenIdContext,
    Dictionary<string, object> Capabilities
) : ICommand;
