// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;

/// <summary>
/// Represents a mediator command to resolve and authenticate the subject identified by a CIBA backchannel
/// authentication request's login hint. Mapping an opaque hint to a local subject is deployment-specific; the default
/// handler reports the request as unresolved (<c>unknown_user_id</c>), and a capability package replaces it.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the backchannel authentication request.</param>
/// <param name="Request">The <see cref="IBackchannelAuthenticationRequest"/> whose subject is being resolved.</param>
[PublicAPI]
public readonly record struct AuthenticateBackchannelRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IBackchannelAuthenticationRequest Request
) : ICommand<AuthenticateSubjectDisposition>;
