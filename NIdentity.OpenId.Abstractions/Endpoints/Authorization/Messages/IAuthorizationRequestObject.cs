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

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the parameters for an <c>OAuth</c> or <c>OpenID Connect</c> authorization request from the request
/// object only.
/// </summary>
public interface IAuthorizationRequestObject : IBaseAuthorizationRequestValues
{
    /// <summary>
    /// Gets a value indicating where the <c>request object</c> was loaded from.
    /// </summary>
    RequestObjectSource RequestObjectSource { get; }
}