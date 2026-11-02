// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
// See ADR-1067.
[PublicAPI]
public readonly record struct DiscoverManagementCapabilitiesCommand(
    OpenIdContext OpenIdContext,
    Dictionary<string, object> Capabilities
) : ICommand;
