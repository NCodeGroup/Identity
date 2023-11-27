#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Logic.Authorization;

namespace NIdentity.OpenId.Endpoints.Authorization.Logic;

internal class NullAuthorizationInteractionService : IAuthorizationInteractionService
{
    /// <inheritdoc />
    public ValueTask<string> GetLoginUrlAsync(
        AuthorizationContext authorizationContext,
        string continueUrl,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask<string> GetCreateAccountUrlAsync(
        AuthorizationContext authorizationContext,
        string continueUrl,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
