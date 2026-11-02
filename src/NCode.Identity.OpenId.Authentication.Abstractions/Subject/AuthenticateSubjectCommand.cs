// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Represents a mediator command that authenticates the subject (the end-user) of the current HTTP request from the
/// interactive session it carries (the host's default authentication scheme, typically the session cookie), returning
/// an <see cref="AuthenticateSubjectDisposition"/>. It serves the interactive endpoints, such as device verification; a
/// request to a protected resource authenticates with an access token through
/// <see cref="AuthenticateResourceRequestCommand"/> instead.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
[PublicAPI]
public readonly record struct AuthenticateSubjectCommand(OpenIdContext OpenIdContext)
    : ICommand<AuthenticateSubjectDisposition>;
