// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;

/// <summary>
/// Represents a mediator command to create and persist the pending CIBA request for an authenticated subject, notify
/// the authentication device, and produce the <see cref="BackchannelAuthenticationResult"/>.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the backchannel authentication request.</param>
/// <param name="Request">The validated <see cref="IBackchannelAuthenticationRequest"/> to persist.</param>
/// <param name="SubjectAuthentication">The <see cref="SubjectAuthentication"/> identifying the resolved end-user the request authenticates.</param>
[PublicAPI]
public readonly record struct CreateBackchannelAuthenticationRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IBackchannelAuthenticationRequest Request,
    SubjectAuthentication SubjectAuthentication
) : ICommand<BackchannelAuthenticationResult>;
