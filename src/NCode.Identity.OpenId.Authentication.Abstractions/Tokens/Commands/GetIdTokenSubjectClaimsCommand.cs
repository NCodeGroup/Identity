// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Commands;

/// <summary>
/// Represents a mediator command that contributes claims about the authenticated subject to an ID token being issued.
/// Each registered handler adds entries to the shared <paramref name="SubjectClaims"/> collection.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> the ID token is issued to.</param>
/// <param name="TokenContext">The <see cref="SecurityTokenContext"/> describing the ID token being generated.</param>
/// <param name="SubjectClaims">The mutable claim collection each handler contributes subject claims to.</param>
[PublicAPI]
public readonly record struct GetIdTokenSubjectClaimsCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    SecurityTokenContext TokenContext,
    ICollection<Claim> SubjectClaims
) : ICommand;
