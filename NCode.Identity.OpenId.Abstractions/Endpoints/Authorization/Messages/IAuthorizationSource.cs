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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Represents an <c>OAuth</c> or <c>OpenID Connect</c> authorization request loaded from query or form data that isn't parsed
/// and only contains string-values.
/// </summary>
[PublicAPI]
public interface IAuthorizationSource : IBaseAuthorizationRequest
{
    /// <summary>
    /// Gets the <see cref="IOpenIdRequestValues"/> for the authorization request.
    /// </summary>
    IOpenIdRequestValues RequestValues { get; }
}
