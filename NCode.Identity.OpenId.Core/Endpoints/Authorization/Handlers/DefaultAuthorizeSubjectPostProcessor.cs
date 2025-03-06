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

using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Mediator.Middleware;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a post-processor for the <see cref="AuthorizeSubjectCommand"/> message.
/// </summary>
public class DefaultAuthorizeSubjectPostProcessor :
    ICommandResponsePostProcessor<AuthorizeSubjectCommand, AuthorizeSubjectDisposition>,
    ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriorityLow;

    /// <inheritdoc />
    public ValueTask PostProcessAsync(
        AuthorizeSubjectCommand command,
        AuthorizeSubjectDisposition response,
        CancellationToken cancellationToken)
    {
        if (response.HasError && response.Error.State == null && command.AuthorizationRequest.State != null)
        {
            response.Error.State = command.AuthorizationRequest.State;
        }

        return ValueTask.CompletedTask;
    }
}
