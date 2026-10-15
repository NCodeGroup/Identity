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

using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message with
/// <see cref="TokenExchangeGrant"/>.
/// </summary>
/// <remarks>
/// The spec-mandated authorization for token exchange (confidential-client requirement, requester entitlement to the
/// presented tokens, and the subject token's <c>may_act</c> authorization of the actor) is enforced directly by
/// <see cref="DefaultTokenExchangeGrantHandler"/> because it requires the validated token payloads. This handler is the
/// mediator seam where a host layers additional per-client exchange policy (for example, restricting the permitted
/// delegation targets); the default performs no additional checks.
/// </remarks>
internal class DefaultValidateTokenExchangeGrantHandler
    : ICommandHandler<ValidateTokenGrantCommand<TokenExchangeGrant>>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenGrantCommand<TokenExchangeGrant> command,
        CancellationToken cancellationToken
    ) => ValueTask.CompletedTask;
}
