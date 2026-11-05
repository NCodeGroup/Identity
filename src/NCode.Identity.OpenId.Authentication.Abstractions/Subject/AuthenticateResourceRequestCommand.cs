// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Represents a mediator command that authenticates the subject (the end-user) of a request to one of the OpenID
/// Provider's own protected resources, such as <c>UserInfo</c>, from the access token the request presents (<c>RFC
/// 6750</c> §2, or a DPoP-bound token per <c>RFC 9449</c> §7.1), returning an <see
/// cref="AuthenticateSubjectDisposition"/>. Only an access token authenticates such a request — never an interactive
/// session — so the subject carries the token's claims, including the <c>client_id</c> of the client it was issued to.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="RequiredAudiences">The audiences the access token must target; the token is rejected unless it targets
/// at least one of them.</param>
// See ADR-1155.
[PublicAPI]
public readonly record struct AuthenticateResourceRequestCommand(
    OpenIdContext OpenIdContext,
    IReadOnlyCollection<string> RequiredAudiences
) : ICommand<AuthenticateSubjectDisposition>;
