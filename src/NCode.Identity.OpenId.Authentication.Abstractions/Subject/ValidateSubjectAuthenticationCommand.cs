#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Represents a mediator command that validates an authenticated subject for an OpenID request, applying the
/// post-authentication checks (for example, <c>max_age</c>, a required ACR, and account state) that must pass before
/// the subject is used to issue tokens. A handler records a failure on <paramref name="OperationDisposition"/> rather
/// than returning a result.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> the request is for.</param>
/// <param name="OpenIdRequest">The <see cref="IOpenIdRequest"/> being processed.</param>
/// <param name="SubjectAuthentication">The <see cref="SubjectAuthentication"/> identifying the authenticated end-user to validate.</param>
/// <param name="OperationDisposition">The disposition a handler records a validation error on when the subject must not proceed.</param>
[PublicAPI]
public readonly record struct ValidateSubjectAuthenticationCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IOpenIdRequest OpenIdRequest,
    SubjectAuthentication SubjectAuthentication,
    OperationDisposition<IOpenIdError> OperationDisposition
) : ICommand;
