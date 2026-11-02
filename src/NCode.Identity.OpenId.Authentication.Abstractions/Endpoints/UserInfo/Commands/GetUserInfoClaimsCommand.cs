// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;

/// <summary>
/// Represents a mediator command to contribute claims about the authenticated subject to a <c>UserInfo</c> response.
/// Multiple handlers may run; the default handler supplies the required <c>sub</c> claim, and applications register
/// additional handlers to add profile, email, and other claims (typically gated by the granted scopes).
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="SubjectAuthentication">The <see cref="SubjectAuthentication"/> identifying the authenticated end-user whose claims are returned.</param>
/// <param name="Claims">The mutable claim set each handler contributes <c>UserInfo</c> claims to.</param>
[PublicAPI]
public readonly record struct GetUserInfoClaimsCommand(
    OpenIdContext OpenIdContext,
    SubjectAuthentication SubjectAuthentication,
    IDictionary<string, object> Claims
) : ICommand;
