﻿#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticatePasswordGrantCommand"/> message
/// that returns <see cref="SubjectAuthentication"/>.
/// </summary>
public class DefaultAuthenticatePasswordGrantHandler : ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectResult>
{
    /// <inheritdoc />
    public ValueTask<AuthenticateSubjectResult> HandleAsync(
        AuthenticatePasswordGrantCommand command,
        CancellationToken cancellationToken)
    {
        // TODO...
        throw new NotImplementedException();
    }
}
