#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

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
