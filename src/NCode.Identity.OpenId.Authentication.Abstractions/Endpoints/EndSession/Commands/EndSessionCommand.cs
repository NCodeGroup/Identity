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
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command that performs OpenID Connect RP-Initiated Logout for a loaded
/// <see cref="IEndSessionRequest"/>: it resolves the subject/session (via <see cref="ResolveEndSessionSubjectCommand"/>),
/// clears the OP login, dispatches <see cref="OnEndSessionCommand"/> to run the logout side effects (grant/session
/// revocation and back-channel notification), and resolves the validated <c>post_logout_redirect_uri</c>. It returns
/// an <see cref="EndSessionDisposition"/> describing where (if anywhere) the user agent should be redirected.
/// </summary>
[PublicAPI]
public readonly record struct EndSessionCommand(
    OpenIdContext OpenIdContext,
    IEndSessionRequest Request
) : ICommand<EndSessionDisposition>;
