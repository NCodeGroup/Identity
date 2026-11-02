// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
