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

using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Messages.Authorization;

#pragma warning disable 1572 // GetAuthorizationRequest.cs(33, 22): [CS1572] XML comment has a param tag for 'HttpContext', but there is no parameter by that name
#pragma warning disable 1573 // GetAuthorizationRequest.cs(34, 55): [CS1573] Parameter 'HttpContext' has no matching param tag in the XML comment for 'GetAuthorizationRequest.GetAuthorizationRequest(HttpContext)' (but other parameters do)
#pragma warning disable 1574 // GetAuthorizationRequest.cs(28, 31): [CS1574] XML comment has cref attribute 'IRequest{TResponse}' that could not be resolved
#pragma warning disable 1591 // GetAuthorizationRequest.cs(34, 55): [CS1591] Missing XML comment for publicly visible type or member 'GetAuthorizationRequest.HttpContext'

namespace NIdentity.OpenId.Requests.Authorization;

/// <summary>
/// Defines an <see cref="IRequest{TResponse}"/> request contract that accepts a <see cref="HttpContext"/> as an
/// input argument and expects <see cref="IAuthorizationRequest"/> as a response.
/// </summary>
/// <param name="HttpContext">The <see cref="HttpContext"/> input argument for the request contract.</param>
public record GetAuthorizationRequest(HttpContext HttpContext) : IRequest<IAuthorizationRequest>;