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
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command dispatched while terminating an end-user session, after the OP login has been cleared.
/// It is the extension point for logout side effects: the default handlers revoke the subject's grants/session and send
/// back-channel logout notifications, and applications add handlers (for example, front-channel logout or custom
/// auditing) by registering additional <see cref="ICommandHandler{TCommand}"/> instances, ordered by
/// <see cref="ISupportMediatorPriority"/>.
/// </summary>
[PublicAPI]
public readonly record struct OnEndSessionCommand(
    OpenIdContext OpenIdContext,
    EndSessionSubject Subject
) : ICommand;
