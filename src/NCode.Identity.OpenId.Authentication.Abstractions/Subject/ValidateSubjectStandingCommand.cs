// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Represents a mediator command that validates the standing of an end user, whether it may sign in or be issued
/// tokens at all whatever the client or the request (for example, a principal an administrator blocked, or a disabled
/// local account). The interactive sign-in sends it before it creates a session, and every validation of a subject's
/// authentication includes it. A handler records a refusal on <paramref name="OperationDisposition"/> rather than
/// returning a result.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="SubjectAuthentication">The <see cref="SubjectAuthentication"/> identifying the end user whose standing is validated.</param>
/// <param name="OperationDisposition">The disposition a handler records a refusal on when the end user must not sign in or be issued tokens.</param>
[PublicAPI]
public readonly record struct ValidateSubjectStandingCommand(
    OpenIdContext OpenIdContext,
    SubjectAuthentication SubjectAuthentication,
    OperationDisposition<IOpenIdError> OperationDisposition
) : ICommand;
