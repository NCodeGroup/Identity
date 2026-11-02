// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;

/// <summary>
/// Represents a mediator command to prompt a subject (the end-user) for consent to the requested scopes. The default
/// handler degrades to an <c>interaction_required</c> error; an interactive consent surface (for example the opt-in
/// login/consent module) handles it by redirecting the user-agent to its consent page, carrying the parked
/// <see cref="ContinueUrl"/> as the return URL so the authorization request resumes after the decision.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
/// <param name="OpenIdClient">The client requesting access.</param>
/// <param name="AuthorizationRequest">The authorization request that requires consent.</param>
/// <param name="ContinueUrl">The local URL that resumes the authorization request once consent is recorded.</param>
[PublicAPI]
public readonly record struct ConsentChallengeCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IAuthorizationRequest AuthorizationRequest,
    string ContinueUrl
) : ICommand<ReadOnlyEndpointDisposition>;
