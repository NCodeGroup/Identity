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
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command that resolves the <see cref="EndSessionSubject"/> (subject, session, requesting
/// client, and participating clients) for an RP-Initiated Logout from the request's <c>id_token_hint</c>, falling back
/// to the current OP login cookie.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="Request">The <see cref="IEndSessionRequest"/> whose <c>id_token_hint</c> identifies the subject.</param>
[PublicAPI]
public readonly record struct ResolveEndSessionSubjectCommand(
    OpenIdContext OpenIdContext,
    IEndSessionRequest Request
) : ICommand<EndSessionSubject>;
