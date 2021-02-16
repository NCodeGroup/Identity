#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using NIdentity.OpenId.Messages.Authorization;

namespace NIdentity.OpenId.Requests
{
    /// <summary>
    /// Defines an <see cref="IRequest"/> request contract that accepts an <see cref="IAuthorizationRequest"/> as an
    /// input argument and doesn't return a value.
    /// </summary>
    /// <param name="AuthorizationRequest">The <see cref="IAuthorizationRequest"/> input argument for the request contract.</param>
    public record ValidateAuthorizationRequest(IAuthorizationRequest AuthorizationRequest) : IRequest;
}
