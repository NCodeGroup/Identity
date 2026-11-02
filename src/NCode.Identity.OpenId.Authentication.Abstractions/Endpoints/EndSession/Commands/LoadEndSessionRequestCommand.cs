// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command that loads and parses the <see cref="IEndSessionRequest"/> for the current HTTP
/// request from its query string or form body.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
[PublicAPI]
public readonly record struct LoadEndSessionRequestCommand(OpenIdContext OpenIdContext)
    : ICommand<IEndSessionRequest>;
