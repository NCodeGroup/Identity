#region Copyright Preamble

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
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Endpoints.Token.Validators;

/// <summary>
/// Provides a default implementation of handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="RefreshTokenGrant"/>.
/// </summary>
public class DefaultValidateRefreshTokenGrantHandler : ICommandHandler<ValidateTokenGrantCommand<RefreshTokenGrant>>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenGrantCommand<RefreshTokenGrant> command,
        CancellationToken cancellationToken)
    {
        // TODO...
        throw new NotImplementedException();
    }
}
