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

#pragma warning disable 1572 // ValidateAuthorizationRequest.cs(28, 22): [CS1572] XML comment has a param tag for 'AuthorizationRequest', but there is no parameter by that name
#pragma warning disable 1573 // ValidateAuthorizationRequest.cs(29, 70): [CS1573] Parameter 'AuthorizationRequest' has no matching param tag in the XML comment for 'ValidateAuthorizationRequest.ValidateAuthorizationRequest(IAuthorizationRequest)' (but other parameters do)
#pragma warning disable 1574 // ValidateAuthorizationRequest.cs(25, 31): [CS1574] XML comment has cref attribute 'IRequest' that could not be resolved
#pragma warning disable 1591 // ValidateAuthorizationRequest.cs(29, 70): [CS1591] Missing XML comment for publicly visible type or member 'ValidateAuthorizationRequest.AuthorizationRequest'

namespace NIdentity.OpenId.Requests
{
    /// <summary>
    /// Defines an <see cref="IRequest"/> request contract that accepts an <see cref="IAuthorizationRequest"/> as an
    /// input argument and doesn't return a value.
    /// </summary>
    /// <param name="AuthorizationRequest">The <see cref="IAuthorizationRequest"/> input argument for the request contract.</param>
    public record ValidateAuthorizationRequest(IAuthorizationRequest AuthorizationRequest) : IRequest;
}
